﻿using System.Collections.Generic;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ByteNuts.NetCoreControls.Services;
using ByteNuts.NetCoreControls.Models.GridView;
using System;
using System.Reflection;
using ByteNuts.NetCoreControls.Models.Enums;
using ByteNuts.NetCoreControls.Extensions;
using ByteNuts.NetCoreControls.Models;
using System.Runtime.Loader;

namespace ByteNuts.NetCoreControls.Controllers
{
    public class NetCoreControlsController : Controller
    {
        private readonly IDataProtector _protector;

        public NetCoreControlsController(IDataProtectionProvider protector)
        {
            _protector = protector.CreateProtector(Constants.DataProtectionKey);
        }

        [HttpPost]
        public IActionResult ControlAction(IFormCollection formCollection, string context, Dictionary<string, string> parameters)
        {
            if (context == null)
                throw new Exception("The control context wasn't submitted. Please, verify if the target id is correct.");

            var controlCtx = JsonService.GetObjectFromJson<object>(_protector.Unprotect(context));

            if (controlCtx == null)
                throw new Exception("The control context is invalid! Please, refresh the page to get a new valid context.");

            var parametersList = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (parameters != null && parameters.Count > 0)
                parametersList = new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase);

            if (!parametersList.ContainsKey($"{DefaultParameters.ActionType.ToString().NccAddPrefix()}"))
                throw new Exception("No action type was specified!");

            var eventHandler = controlCtx.NccGetPropertyValue<string>("EventHandlerClass");
            var service = ReflectionService.NccGetClassInstance(eventHandler, null);

            service?.NccInvokeMethod(NccEvents.PostBack, new object[] { new NccEventArgs { Controller = this, NccControlContext = controlCtx, FormCollection = formCollection } });

            switch (parametersList[$"{DefaultParameters.ActionType.ToString().NccAddPrefix()}"].ToLower())
            {
                case "filter":
                    if (!(parametersList.ContainsKey($"{DefaultParameters.ElemName.ToString().NccAddPrefix()}") && parametersList.ContainsKey($"{DefaultParameters.ElemValue.ToString().NccAddPrefix()}")))
                        throw new Exception("The filter doesn't contain the necessary name and value pair.");

                    var filters = controlCtx.NccGetPropertyValue<Dictionary<string, string>>("Filters") ?? new Dictionary<string, string>();
                    filters[parametersList[$"{DefaultParameters.ElemName.ToString().NccAddPrefix()}"]] = parametersList[$"{DefaultParameters.ElemValue.ToString().NccAddPrefix()}"];

                    controlCtx.NccSetPropertyValue("Filters", filters);
                    break;

                case "event":
                    if (service == null) throw new Exception("EventHandler must be registered and must exist to process events");
                    EventService.ProcessEvent(service, ControllerContext, controlCtx, formCollection, parametersList);
                    break;
                    //Events from Controls are mapped here
                case "gridviewevent":
                    if (!parametersList.ContainsKey($"{DefaultParameters.EventName.ToString().NccAddPrefix()}"))
                        throw new Exception("No EventName specified for the GridView action!");
                    if (!(controlCtx is GridViewContext))
                        throw new Exception("A GridViewAction was specified but the context is not of type GridViewContext!");
                    switch (parametersList[$"{DefaultParameters.EventName.ToString().NccAddPrefix()}"].ToLower())
                    {
                        case "onupdate":
                            if (service == null) throw new Exception("EventHandler must be registered and must exist to process events");
                            service.NccInvokeMethod(GridViewEvents.Update, new object[] { new NccEventArgs { Controller = this, NccControlContext = controlCtx, FormCollection = formCollection } });
                            break;
                        case "onupdaterow":
                            break;
                        case "ondeleterow":
                            if (!parametersList.ContainsKey($"{GridViewParameters.RowNumber.ToString().NccAddPrefix()}"))
                                throw new Exception("The row number wasn't received... Something wrong has happened...");

                            var rowPos = Convert.ToInt32(parametersList[$"{GridViewParameters.RowNumber.ToString().NccAddPrefix()}"]);

                            if (service == null) throw new Exception("EventHandler must be registered and must exist to process events");
                            service.NccInvokeMethod(GridViewEvents.DeleteRow, new object[] { new NccEventArgs { Controller = this, NccControlContext = controlCtx, FormCollection = formCollection }, rowPos });

                            break;
                        default:
                            throw new Exception("The specified EventName it's not supported on the GridView Component!");
                    }
                    break;
                default:
                    throw new Exception("The specified ActionType it's not supported on the NetCoreControls!");
            }


            var id = controlCtx.NccGetPropertyValue<string>("Id");
            var controlViewPath = controlCtx.NccGetPropertyValue<ViewsPathsModel>("ViewPaths").ViewPath;
            ViewData[id] = controlCtx;

            return PartialView(controlViewPath);
        }

        public FileResult GetNccJsFile()
        {
            var assembly = this.GetType().GetTypeInfo().Assembly;
            //(typeof(NetCoreControls.Constants).Assembly).GetManifestResourceStream("NetCoreControls.Scripts.ncc.js")

            var stream = assembly.GetManifestResourceStream("NetCoreControls.Scripts.ncc.js");


            return File(stream, "application/javascript");
        }
    }
}