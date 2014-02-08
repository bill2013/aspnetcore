﻿using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNet.Abstractions;

namespace Microsoft.AspNet.Mvc
{
    public class ControllerActionInvoker : IActionInvoker
    {
        private readonly RequestContext _requestContext;
        private readonly ControllerActionRouteContext _descriptor;
        private readonly IActionResultFactory _actionResultFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly IControllerFactory _controllerFactory;

        public ControllerActionInvoker(RequestContext requestContext,
                                       ControllerActionRouteContext descriptor,
                                       IActionResultFactory actionResultFactory,
                                       IControllerFactory controllerFactory,
                                       IServiceProvider serviceProvider)
        {
            _requestContext = requestContext;
            _descriptor = descriptor;
            _actionResultFactory = actionResultFactory;
            _controllerFactory = controllerFactory;
            _serviceProvider = serviceProvider;
        }

        public Task InvokeActionAsync()
        {
            IActionResult actionResult = null;

            object controller = _controllerFactory.CreateController(_requestContext.HttpContext, _descriptor.ControllerName);

            if (controller == null)
            {
                actionResult = new HttpStatusCodeResult(404);
            }
            else
            {
                Initialize(controller);

                var method = controller.GetType().GetRuntimeMethods().FirstOrDefault(m => m.Name.Equals(_descriptor.ActionName, StringComparison.OrdinalIgnoreCase));

                if (method == null)
                {
                    throw new InvalidOperationException(String.Format("Could not find action method '{0}'", _descriptor.ActionName));
                }

                object actionReturnValue = method.Invoke(controller, null);

                actionResult = _actionResultFactory.CreateActionResult(method.ReturnType, actionReturnValue, _requestContext);
            }

            // TODO: This will probably move out once we got filters
            return actionResult.ExecuteResultAsync(_requestContext);
        }

        private void Initialize(object controller)
        {
            var controllerType = controller.GetType();

            foreach (var prop in controllerType.GetRuntimeProperties())
            {
                if (prop.Name == "Context")
                {
                    if (prop.PropertyType == typeof(HttpContext))
                    {
                        prop.SetValue(controller, _requestContext.HttpContext);
                    }
                }
            }

            var method = controllerType.GetRuntimeMethods().FirstOrDefault(m => m.Name.Equals("Initialize", StringComparison.OrdinalIgnoreCase));

            if (method == null)
            {
                return;
            }

            var args = method.GetParameters()
                             .Select(p => _serviceProvider.GetService(p.ParameterType)).ToArray();

            method.Invoke(controller, args);
        }
    }
}
