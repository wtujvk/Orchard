﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using Orchard.ContentManagement;
using Orchard.DisplayManagement;
using Orchard.Environment.Extensions;
using Orchard.ImportExport.Permissions;
using Orchard.ImportExport.Services;
using Orchard.Localization;
using Orchard.Logging;
using Orchard.UI.Admin;
using Orchard.UI.Navigation;
using Orchard.UI.Notify;

namespace Orchard.ImportExport.Controllers {
    [Admin]
    [OrchardFeature("Orchard.Deployment")]
    public class DeploymentConfigurationController : Controller, IUpdateModel {
        private readonly IDeploymentService _deploymentService;

        public DeploymentConfigurationController(IOrchardServices services,
            IDeploymentService deploymentService,
            IShapeFactory shapeFactory) {
            _deploymentService = deploymentService;
            Services = services;
            T = NullLocalizer.Instance;
            Logger = NullLogger.Instance;
            Shape = shapeFactory;
        }

        public IOrchardServices Services { get; private set; }
        public Localizer T { get; set; }
        public ILogger Logger { get; set; }
        private dynamic Shape { get; set; }

        public ActionResult Index(PagerParameters pagerParameters) {
            if (!Services.Authorizer.Authorize(DeploymentPermissions.ConfigureDeployments, T("Not allowed to configure deployments.")))
                return new HttpUnauthorizedResult();

            var pager = new Pager(Services.WorkContext.CurrentSite, pagerParameters);

            var sourceTargetTypes = _deploymentService.GetDeploymentConfigurationContentTypes().Select(c => c.Name).ToArray();

            var configs = sourceTargetTypes.Any() ?
                Services.ContentManager.Query(sourceTargetTypes).List<IContent>().ToList() : new List<IContent>();

            var pagerShape = Shape.Pager(pager).TotalItemCount(configs.Count());

            configs = configs
                .Skip(pager.GetStartIndex())
                .Take(pager.PageSize)
                .ToList();

            dynamic viewModel = Shape.ViewModel()
                .ContentItems(configs)
                .Pager(pagerShape);

            return View(viewModel);
        }

        public ActionResult Create() {
            if (!Services.Authorizer.Authorize(DeploymentPermissions.ConfigureDeployments, T("Not allowed to configure deployments.")))
                return new HttpUnauthorizedResult();

            var sourceAndTargetTypes = _deploymentService.GetDeploymentConfigurationContentTypes().ToArray();

            dynamic viewModel = Shape.ViewModel(ContentTypes: sourceAndTargetTypes);

            // Casting to avoid invalid (under medium trust) reflection over the protected View method and force a static invocation.
            return View("CreatableTypeList", (object) viewModel);
        }

        public ActionResult TestConnection(int id) {
            if (!Services.Authorizer.Authorize(DeploymentPermissions.ConfigureDeployments, T("Not allowed to configure deployments.")))
                return new HttpUnauthorizedResult();

            try {
                var deploymentSource = _deploymentService.GetDeploymentSource(Services.ContentManager.Get(id));
                if (deploymentSource != null) {
                    deploymentSource.GetContentTypes();
                    Services.Notifier.Add(NotifyType.Information, T("Successfully tested import from remote target."));
                }
            }
            catch (WebException ex) {
                Services.Notifier.Add(NotifyType.Warning,T
                    ("Unable to import from deployment source. Review configuration and ensure that features are enabled, deployment source is configured with required permissions for deployment export of content, and that both servers have the same API key.<br/>{0}", ex.Message));
                Logger.Information(ex, "Deployment import connection test failed.");
            }

            try {
                var deploymentTarget = _deploymentService.GetDeploymentTarget(Services.ContentManager.Get(id));
                if (deploymentTarget != null) {
                    deploymentTarget.PushRecipe(Guid.NewGuid().ToString("n"), @"<Orchard><Recipe></Recipe></Orchard>");
                    Services.Notifier.Add(NotifyType.Information, T("Successfully tested deployment to remote target."));
                }
            }
            catch (WebException ex) {
                Services.Notifier.Add(NotifyType.Warning,
                    T("Unable to deploy content to target. Review configuration and ensure that features are enabled, deployment target is configured with required permissions for deployment import of content, and that both servers have the same API key.<br/>{0}", ex.Message));
                Logger.Information(ex, "Deployment export connection test failed.");
            }

            return RedirectToAction("Index");
        }

        bool IUpdateModel.TryUpdateModel<TModel>(TModel model, string prefix, string[] includeProperties, string[] excludeProperties) {
            return TryUpdateModel(model, prefix, includeProperties, excludeProperties);
        }

        public void AddModelError(string key, LocalizedString errorMessage) {
            ModelState.AddModelError(key, errorMessage.ToString());
        }
    }
}