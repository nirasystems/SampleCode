#region Header
//      Copyright (C) 2018 NIRA Systems.
//      All rights reserved. Reproduction or transmission in whole or in part, in
//      any form or by any means, electronic, mechanical or otherwise, is prohibited
//      without the prior written consent of the copyright owner.
//
// Release history:
//      Date        |    Author             |       Description
//----------------------------------------------------------------------------------------------
//      16-Oct-2018 |    Umesh Babu         |       Created and implemented the functionalities. 
//----------------------------------------------------------------------------------------------
#endregion

#region Namespaces
using Newtonsoft.Json;
using Nirast.Dart.MongoDBClient;
using Nirast.RadiologyDashboard.Logger;
using Nirast.RadiologyDashboard.Models;
using Nirast.RadiologyDashboard.Radiology;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
#endregion

namespace Nirast.RadiologyDashboard.Controllers
{
    #region Class
    public class RadiologySitesController : Controller
    {
        #region Private Members
        /// <summary>
        /// Database entity object
        /// </summary>
        private RadiologyEntities dbEntity;
        #endregion

        #region Public Members
        #endregion

        #region Constructor
        /// <summary>
        /// The Constructor.
        /// </summary>
        public RadiologySitesController()
        {
            dbEntity = new RadiologyEntities();
        }
        #endregion

        #region Action Methods
        /// <summary>
        /// Default index action of the controller
        /// </summary>
        /// <returns>Returns the result.</returns>
        public ActionResult Index()
        {
			List<CDSOrganizationViewModel> cdsOrganizationVM = new List<CDSOrganizationViewModel>();
			DateTime cdsstartingdate = new DateTime(2018, 3, 1);

			foreach (var item in dbEntity.CDSOrganizations.ToList())
			{
				CDSOrganizationViewModel cdsOrgVM = new Models.CDSOrganizationViewModel();
				cdsOrgVM.CreatedDate = item.CreatedDate;
				cdsOrgVM.ID = item.ID;
				cdsOrgVM.ModifiedDate = item.ModifiedDate;
				cdsOrgVM.Organization = item.Organization;
				cdsOrgVM.OrganizationID = item.OrganizationID;
				cdsOrgVM.SecurityKey = item.SecurityKey;
				cdsOrgVM.GroupsIngestDate = (item.GroupIngestedDate == null) ? cdsstartingdate : (DateTime)item.GroupIngestedDate;
				cdsOrgVM.CDSOrganizationGroupList = dbEntity.CDSOrganizationGroups.Where(x => x.OrganizationID == item.OrganizationID).ToList();
				cdsOrganizationVM.Add(cdsOrgVM);
			}
			return View(cdsOrganizationVM);
        }

        /// <summary>
        /// Update sub groups.
        /// </summary>
        /// <returns>Returns the result.</returns>
		[HttpPost]
		public JsonResult UpdateSubGroups()
		{
			bool data = false;
			string personifyID = (string)Session["CustomerID"];

			try
			{
				//Dictionary<string, string> CDSOrgGroup = new Dictionary<string, string>();
				List<RadiologyMongoDBWrapper.CDSInfo> LcDSInfo = new List<RadiologyMongoDBWrapper.CDSInfo>();

				var CDSOrganizations = dbEntity.CDSOrganizations;
				RadiologyMongoDBWrapper rscanMongoDBWrapper = new RadiologyMongoDBWrapper();

				foreach (var item in CDSOrganizations.ToList())
				{
					// If has updated already then skip
					if ((item.GroupIngestedDate == null) || (item.GroupIngestedDate <= DateTime.Now.AddDays(-1)))
					{
						LcDSInfo.Clear();

						// First Time Update
						if (item.GroupIngestedDate == null)
							item.GroupIngestedDate = Convert.ToDateTime("2016-03-01");

						Task.Run(async () => { LcDSInfo = await rscanMongoDBWrapper.CDSOrgGroups(item.OrganizationID, (DateTime)item.GroupIngestedDate, "RadiologyIngest"); }).Wait(); //CDSOrgGroup

						foreach (var item1 in LcDSInfo)//CDSOrgGroup
						{
							// Make sure there is no duplicate groupid in orgid in sql
							if (dbEntity.CDSOrganizationGroups.Where(x => x.OrganizationID == item.OrganizationID && x.GroupId.Trim() == item1.GroupId).Count() == 0)//item1.Key
							{
								CDSOrganizationGroup cdsorggroup = new CDSOrganizationGroup();
								cdsorggroup.OrganizationID = item.OrganizationID;								
								cdsorggroup.GroupId = item1.GroupId;
								cdsorggroup.GroupName = item1.GroupName;
								cdsorggroup.DepartmentId = item1.DepartmentId;
								dbEntity.CDSOrganizationGroups.Add(cdsorggroup);
								dbEntity.SaveChanges();
							}
						}

						// Update GroupIngestedDate
						CDSOrganization org = dbEntity.CDSOrganizations.Where(x => x.OrganizationID == item.OrganizationID).FirstOrDefault();
						org.GroupIngestedDate = DateTime.Now;
						dbEntity.CDSOrganizations.Add(org);
						dbEntity.Entry(org).State = EntityState.Modified;
						dbEntity.SaveChanges();
						RadiologyLog.Info("CDSSitesController","UpdateSubGroups by " + personifyID);
						data = true;
					}
				}
			}
			catch (Exception ex)
			{
				RadiologyLog.Error("CDSSitesController", "UpdateSubGroups issues is " + ex);
				RedirectToAction("Error", "Home");
			}
			return Json(data, JsonRequestBehavior.AllowGet);
		}

        /// <summary>
        /// Convert to CDS object.
        /// </summary>
        /// <param name="tentativeId">Tentative ID</param>
        /// <param name="organizationId">Organization ID</param>
        /// <param name="organizationName">Organization Name</param>
        /// <param name="pword">Ndsc Key</param>
        /// <returns>Returns the result.</returns>
		[HttpPost]
		public JsonResult ConvertCDS(int tentativeId, int organizationId, string organizationName, string pword)
		{
			string personifyID = (string)Session["CustomerID"];
			bool data = false;
			try
			{
				var cdsOrg = dbEntity.CDSOrganizations.Where(x => x.Organization == org_name).FirstOrDefault();
				if (cdsOrg == null)
				{
					RadiologyMongoDBWrapper RmdWrapper = new RadiologyMongoDBWrapper();
					AESCryptoManager aesCryptoManager = new AESCryptoManager();
					var originalPassword = aesCryptoManager.EncryptText(pword, RadiologyConstants.Salt);
					DateTime cdsstartingdate = new DateTime(2016, 3, 1);

					CDSOrganization cdsOrganization = new Models.CDSOrganization();
					cdsOrganization.SecurityKey = originalPassword;
					cdsOrganization.CreatedDate = DateTime.Now;
					cdsOrganization.Organization = organizationName.Trim();
					cdsOrganization.OrganizationID = organizationId;
					cdsOrganization.GroupIngestedDate = cdsstartingdate;
					dbEntity.CDSOrganizations.Add(cdsOrganization);

					CDSTentativeSite cDSTentative = dbEntity.CDSTentativeSites.Find(tentativeId);
					dbEntity.CDSTentativeSites.Remove(cDSTentative);

					dbEntity.SaveChanges();

					if (cdsOrganization.ID > 0)
					{
						Dictionary<string, object> insertData = new Dictionary<string, object>();
						insertData.Add("SecurityKey", originalPassword);
						insertData.Add("LastUpdateDate", cdsstartingdate.ToShortDateString());
						insertData.Add("Location", organizationName.Trim());
						insertData.Add("OrganizationId", organizationId);
						string jsonDataToInsert = JsonConvert.SerializeObject(insertData);
						Task.Run(async () => { await RmdWrapper.InsertCollection2(jsonDataToInsert, "UserInfo"); }).Wait();
					}

					var cdsRegistry = dbEntity.CDSRegistryProjects.Where(x => x.TentativeSiteID == tentativeId).FirstOrDefault();
					if (cdsRegistry != null)
					{
						cdsRegistry.SiteID = cdsOrganization.ID;
						cdsRegistry.TentativeSiteID = null;
						dbEntity.SaveChanges();
					}

					RadiologyLog.Info("CDSSitesController", "ConvertCDS by " + personifyID);
					data = true;
				}

			}
			catch (Exception ex)
			{
				RadiologyLog.Error("CDSSitesController", "ConvertCDS issues is " + ex);
				RedirectToAction("Error", "Home");
			}
			return Json(data, JsonRequestBehavior.AllowGet);
		}

		/// <summary>
        /// Get the organization details
        /// </summary>
        /// <param name="id">Organization ID</param>
        /// <returns>Returns the result.</returns>
		[HttpGet]
		public ActionResult GetDetails(int? id)
        {
			string personifyID = (string)Session["CustomerID"];
			if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
			
            CDSOrganization cDSOrganization = dbEntity.CDSOrganizations.Find(id);

			CDSOrganizationViewModel cdsOrgVM = new CDSOrganizationViewModel();
			cdsOrgVM.CreatedDate = cDSOrganization.CreatedDate;
			cdsOrgVM.ID = cDSOrganization.ID;
			cdsOrgVM.ModifiedDate = cDSOrganization.ModifiedDate;
			cdsOrgVM.Organization = cDSOrganization.Organization;
			cdsOrgVM.OrganizationID = cDSOrganization.OrganizationID;
			cdsOrgVM.SecurityKey = cDSOrganization.SecurityKey;
			if (cDSOrganization == null)
            {
                return HttpNotFound();
            }

			RadiologyLog.Info("CDSSitesController", "Details: " + id + ", by" + personifyID);
			return View(cdsOrgVM);
        }

		/// <summary>
		/// Get the list of CDS participants.
		/// </summary>
		/// <returns>Returns the result.</returns>
		[HttpGet]
		public ActionResult GetCDSParticipants()
		{
			return PartialView(dbEntity.CDSReportingAccesses.ToList());
		}

		/// <summary>
        /// Create an organization.
        /// </summary>
        /// <param name="cDSOrganization">The Organization.</param>
        /// <returns>Returns the result.</returns>
		[HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Include = "ID,OrganizationID,Organization,SecurityKey,CreatedDate,ModifiedDate")] CDSOrganizationViewModel cDSOrganization)
        {
			string personifyID = (string)Session["CustomerID"];

            if (Session["AdminUser"].ToString() != RadiologyConstants.AdminUser)
            {
                return RedirectToAction("Index");
            }

            if (ModelState.IsValid)
            {
                var cdsOrg = dbEntity.CDSOrganizations.Where(x => x.OrganizationID == cDSOrganization.OrganizationID).FirstOrDefault();
                if (cdsOrg != null)
                {
                    ViewBag.ErrorMessage = "Organization ID already exist";
                }

                RadiologyMongoDBWrapper RmdWrapper = new RadiologyMongoDBWrapper();
                AESCryptoManager aesCryptoManager = new AESCryptoManager();
                var originalPassword = aesCryptoManager.EncryptText(cDSOrganization.SecurityKey, RadiologyConstants.Salt);
                CDSOrganization cdsOrganization = new Models.CDSOrganization();
                cdsOrganization.SecurityKey = originalPassword;
                cdsOrganization.CreatedDate = DateTime.Now;
                cdsOrganization.Organization = cDSOrganization.Organization.Trim();
                cdsOrganization.OrganizationID = cDSOrganization.OrganizationID;
                dbEntity.CDSOrganizations.Add(cdsOrganization);
                dbEntity.SaveChanges();

                if (cdsOrganization.ID > 0)
                {
                    Dictionary<string, object> insertData = new Dictionary<string, object>();
                    insertData.Add("SecurityKey", originalPassword);
                    DateTime cdsstartingdate = new DateTime(2016, 3, 1);
                    insertData.Add("LastUpdateDate", cdsstartingdate.ToShortDateString());
                    insertData.Add("Location", cDSOrganization.Organization.Trim());
                    insertData.Add("OrganizationId", cdsOrganization.OrganizationID);
                    string jsonDataToInsert = JsonConvert.SerializeObject(insertData);
                    Task.Run(async () => { await RmdWrapper.InsertCollection2(jsonDataToInsert, "UserInfo"); }).Wait();
                }

                RadiologyLog.Info("CDSSitesController", "Create Orgnization: " + cDSOrganization.Organization.Trim() + ", Org id: " + cdsOrganization.ID + ", by " + personifyID);
                return RedirectToAction("Index");
            }

            return View(cDSOrganization);
        }

        /// <summary>
        /// The organization edit
        /// </summary>
        /// <param name="id">The Organization ID</param>
        /// <returns>Returns the result.</returns>
		[HttpPost]
        public ActionResult Edit(int? id)
        {
            if (Session["AdminUser"].ToString() != RadiologyConstants.AdminUser)
            {
                return RedirectToAction("Index");
            }

			AESCryptoManager aesCryptoManager = new AESCryptoManager();
			if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            CDSOrganization cds_Organization = dbEntity.CDSOrganizations.Find(id);
			var originalPassword = aesCryptoManager.DecryptText(cds_Organization.SecurityKey, RadiologyConstants.Salt);
			if (cDSOrganization == null)
            {
                return HttpNotFound();
            }

			CDSOrganizationViewModel cdsOrganization = new Models.CDSOrganizationViewModel();
            cdsOrganization.CreatedDate = cds_Organization.CreatedDate;
            cdsOrganization.ModifiedDate = DateTime.Now;
			cdsOrganization.Organization = cds_Organization.Organization;
			cdsOrganization.OrganizationID = cds_Organization.OrganizationID;
			cdsOrganization.SecurityKey = originalPassword;

			return View(cdsOrganization);
        }
        
        /// <summary>
        /// The organization edit.
        /// </summary>
        /// <param name="cdsOrganization">Organization</param>
        /// <returns>Returns the result.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "ID,OrganizationID,Organization,SecurityKey,CreatedDate,ModifiedDate")] CDSOrganizationViewModel cdsOrganization)
        {
			string personifyID = (string)Session["CustomerID"];

            if (Session["AdminUser"].ToString() != RadiologyConstants.AdminUser)
            {
                return RedirectToAction("Index");
            }

			string result = null;			
            RadiologyMongoDBWrapper rmdWrapper = new RadiologyMongoDBWrapper();
			AESCryptoManager aesCryptoManager = new AESCryptoManager();

			if (ModelState.IsValid)
            {
				Task.Run(async () => { result = await rmdWrapper.FindId("OrganizationId", cdsOrganization.OrganizationID, "UserInfo"); }).Wait();

                if (!string.IsNullOrEmpty(result))
                {
                    var originalPassword = aesCryptoManager.EncryptText(cdsOrganization.SecurityKey, RadiologyConstants.Salt);

                    Task.Run(async () => { await rmdWrapper.UpdateFields("OrganizationId", cdsOrganization.OrganizationID, "Location", cdsOrganization.Organization.Trim(), "SecurityKey", originalPassword, "UserInfo"); }).Wait();

                    CDSOrganization cDSOrganization = new CDSOrganization();
                    cDSOrganization.SecurityKey = originalPassword;
                    cDSOrganization.ID = cdsOrganization.ID;
                    cDSOrganization.Organization = cdsOrganization.Organization.Trim();
                    cDSOrganization.OrganizationID = cdsOrganization.OrganizationID;
                    cDSOrganization.CreatedDate = cdsOrganization.CreatedDate;
                    cDSOrganization.ModifiedDate = DateTime.Now;
                    dbEntity.Entry(cDSOrganization).State = EntityState.Modified;
                    dbEntity.SaveChanges();

					RadiologyLog.Info("CDSSitesController", "Edit Orgnization: " + cDSOrganization.Organization.Trim() + ", Org id: " + cdsOrganization.ID + " by" + personifyID);
				}
				return RedirectToAction("Index");
            }
            return View(cdsOrganization);
        }

        /// <summary>
        /// Find hte organization. 
        /// </summary>
        /// <param name="id">Organization Id</param>
        /// <returns>Returns the Organization details.</returns>
		[HttpGet]
        public ActionResult Find(int? id)
        {
            if (Session["AdminUser"].ToString() != RadiologyConstants.AdminUser)
            {
                return RedirectToAction("Index");
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            CDSOrganization cDSOrganization = dbEntity.CDSOrganizations.Find(id);
			if (cDSOrganization == null)
            {
                return HttpNotFound();
            }

            return View(cDSOrganization);
        }

        /// <summary>
        /// Confirm to delete.
        /// </summary>
        /// <param name="id">Organization ID</param>
        /// <returns>Returns the result.</returns>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
			string personifyID = (string)Session["CustomerID"];
            if (Session["AdminUser"].ToString() != RadiologyConstants.AdminUser)
            {
                return RedirectToAction("Index");
            }

            string result = string.Empty;
			RadiologyMongoDBWrapper rmdWrapper = new RadiologyMongoDBWrapper();
            CDSOrganization cDSOrganization = dbEntity.CDSOrganizations.Find(id);

			List<CDSOrganizationGroup> cDSOrgGroupsList = dbEntity.CDSOrganizationGroups.Where(x => x.OrganizationID == cDSOrganization.OrganizationID).ToList();
            foreach (var cdsgroup in cDSOrgGroupsList.ToList())
            {
                dbEntity.CDSOrganizationGroups.Remove(cdsgroup);
                dbEntity.SaveChanges();
            }

			Task.Run(async () => { await rmdWrapper.DeleteOneCollection2("OrganizationId", cDSOrganization.OrganizationID, "UserInfo"); }).Wait();
			dbEntity.CDSOrganizations.Remove(cDSOrganization);
            dbEntity.SaveChanges();
			RadiologyLog.Info("CDSSitesController", "DeleteConfirmed, id: " + id + ", by " + personifyID);

			return RedirectToAction("Index");
        }
        #endregion

        #region Private Methods
        #endregion

        #region Dispose Method
        /// <summary>
        /// The dispose method.
        /// </summary>
        /// <param name="disposing">The disposing</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                dbEntity.Dispose();
            }
            base.Dispose(disposing);
        }
        #endregion
    }
    #endregion
}
