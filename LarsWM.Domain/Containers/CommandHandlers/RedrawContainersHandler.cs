﻿using System;
using System.Collections.Generic;
using System.Linq;
using LarsWM.Domain.Common.Enums;
using LarsWM.Domain.Common.Models;
using LarsWM.Domain.Containers.Commands;
using LarsWM.Domain.UserConfigs;
using LarsWM.Domain.Windows;
using LarsWM.Infrastructure.Bussing;
using static LarsWM.Infrastructure.WindowsApi.WindowsApiService;

namespace LarsWM.Domain.Containers.CommandHandlers
{
    class RedrawContainersHandler : ICommandHandler<RedrawContainersCommand>
    {
        private ContainerService _containerService;
        private UserConfigService _userConfigService;

        public RedrawContainersHandler(ContainerService containerService, UserConfigService userConfigService)
        {
            _containerService = containerService;
            _userConfigService = userConfigService;
        }

        public dynamic Handle(RedrawContainersCommand command)
        {
            var splitContainersToRedraw = _containerService.SplitContainersToRedraw;

            // Enumerable of all split containers to redraw (including nested split containers).
            var allSplitContainersToRedraw = splitContainersToRedraw
                .SelectMany(container => container.Flatten())
                .OfType<SplitContainer>()
                .Distinct();

            var innerGap = _userConfigService.UserConfig.InnerGap;

            foreach (var parentContainer in allSplitContainersToRedraw)
            {
                var children = parentContainer.Children;

                if (parentContainer.Layout == Layout.Horizontal)
                {
                    // Available parent width is the width of the parent minus all inner gaps.
                    var availableParentWidth = parentContainer.Width - (innerGap * (children.Count - 1));

                    // Adjust size and location of child containers.
                    Container previousChild = null;
                    foreach (var child in children)
                    {
                        // Direct children of parent have the same height and Y coord as parent in horizontal layouts.
                        child.Height = parentContainer.Height;
                        child.Y = parentContainer.Y;

                        child.Width = (int)(child.SizePercentage * availableParentWidth);

                        if (previousChild == null)
                            child.X = parentContainer.X;

                        else
                            child.X = previousChild.X + previousChild.Width + innerGap;

                        previousChild = child;
                    }
                }

                if (parentContainer.Layout == Layout.Vertical)
                {
                    // Available parent height is the height of the parent minus all inner gaps.
                    var availableParentHeight = parentContainer.Height - (innerGap * (children.Count - 1));

                    // Adjust size and location of child containers.
                    Container previousChild = null;
                    foreach (var child in children)
                    {
                        // Direct children of parent have the same width and X coord as parent in vertical layouts.
                        child.Width = parentContainer.Width;
                        child.X = parentContainer.X;

                        child.Height = (int)(child.SizePercentage * availableParentHeight);

                        if (previousChild == null)
                            child.Y = parentContainer.Y;

                        else
                            child.Y = previousChild.Y + previousChild.Height + innerGap;

                        previousChild = child;
                    }
                }
            }

            PushUpdates(splitContainersToRedraw);

            return CommandResponse.Ok;
        }

        private void PushUpdates(List<SplitContainer> containersToRedraw)
        {
            var windowsToRedraw = containersToRedraw
                .SelectMany(container => container.Flatten())
                .OfType<Window>()
                .Distinct()
                .ToList();

            var handle = BeginDeferWindowPos(windowsToRedraw.Count());

            foreach (var window in windowsToRedraw)
            {
                var flags = SWP.SWP_FRAMECHANGED | SWP.SWP_NOACTIVATE | SWP.SWP_NOCOPYBITS |
                    SWP.SWP_NOZORDER | SWP.SWP_NOOWNERZORDER;

                if (window.IsHidden)
                    flags |= SWP.SWP_HIDEWINDOW;
                else
                    flags |= SWP.SWP_SHOWWINDOW;

                DeferWindowPos(handle, window.Hwnd, IntPtr.Zero, window.X, window.Y, window.Width, window.Height, flags);
            }

            EndDeferWindowPos(handle);

            containersToRedraw.Clear();
        }
    }
}
