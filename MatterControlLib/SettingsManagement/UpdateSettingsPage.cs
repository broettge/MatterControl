﻿/*
Copyright (c) 2018, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public partial class UpdateSettingsPage : DialogPage
	{
		private PrinterConfig printer;

		public UpdateSettingsPage(PrinterConfig printer)
			: base("Close".Localize())
		{
			this.printer = printer;
			this.AlwaysOnTopOfMain = true;
			this.WindowTitle = this.HeaderText = "Update Settings".Localize();
			this.WindowSize = new Vector2(700 * GuiWidget.DeviceScale, 600 * GuiWidget.DeviceScale);

			contentRow.Padding = theme.DefaultContainerPadding;
			contentRow.Padding = 0;
			contentRow.BackgroundColor = Color.Transparent;
			GuiWidget settingsColumn;

			{
				var settingsAreaScrollBox = new ScrollableWidget(true);
				settingsAreaScrollBox.ScrollArea.HAnchor |= HAnchor.Stretch;
				settingsAreaScrollBox.AnchorAll();
				settingsAreaScrollBox.BackgroundColor = theme.MinimalShade;
				contentRow.AddChild(settingsAreaScrollBox);

				settingsColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					HAnchor = HAnchor.MaxFitOrStretch
				};

				settingsAreaScrollBox.AddChild(settingsColumn);
			}

			AddUpgradeInfoPannel(settingsColumn);

			AddUsserOptionsPannel(settingsColumn);

			AddAdvancedPannel(settingsColumn);

			// Enforce consistent SectionWidget spacing and last child borders
			foreach (var section in settingsColumn.Children<SectionWidget>())
			{
				section.Margin = new BorderDouble(0, 10, 0, 0);

				if (section.ContentPanel.Children.LastOrDefault() is SettingsItem lastRow)
				{
					// If we're in a contentPanel that has SettingsItems...

					// Clear the last items bottom border
					lastRow.Border = lastRow.Border.Clone(bottom: 0);

					// Set a common margin on the parent container
					section.ContentPanel.Margin = new BorderDouble(2, 0);
				}
			}
		}

		private void AddUpgradeInfoPannel(GuiWidget settingsColumn)
		{
			var generalPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};

			var configureIcon = AggContext.StaticData.LoadIcon("fa-cog_16.png", 16, 16, theme.InvertIcons);

			var updateSection = new SectionWidget("Update".Localize(), generalPanel, theme, expandingContent: false)
			{
				Name = "Update Section",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};
			settingsColumn.AddChild(updateSection);

			theme.ApplyBoxStyle(updateSection);

			// Print Notifications
			var configureNotificationsButton = new IconButton(configureIcon, theme)
			{
				Name = "Configure Notification Settings Button",
				ToolTipText = "Configure Notifications".Localize(),
				Margin = new BorderDouble(left: 6),
				VAnchor = VAnchor.Center
			};
			configureNotificationsButton.Click += (s, e) =>
			{
				if (ApplicationController.ChangeToPrintNotification != null)
				{
					UiThread.RunOnIdle(() =>
					{
						ApplicationController.ChangeToPrintNotification(this.DialogWindow);
					});
				}
			};

			this.AddSettingsRow(
				new SettingsItem(
					"Notifications".Localize(),
					theme,
					new SettingsItem.ToggleSwitchConfig()
					{
						Checked = UserSettings.Instance.get(UserSettingsKey.PrintNotificationsEnabled) == "true",
						ToggleAction = (itemChecked) =>
						{
							UserSettings.Instance.set(UserSettingsKey.PrintNotificationsEnabled, itemChecked ? "true" : "false");
						}
					},
					configureNotificationsButton,
					AggContext.StaticData.LoadIcon("notify-24x24.png", 16, 16, theme.InvertIcons)),
				generalPanel);

			foreach (var localOemSetting in printer.Settings.OemLayer)
			{
				if (!ignoreSettings.Contains(localOemSetting.Key)
					&& !PrinterSettingsExtensions.SettingsToReset.ContainsKey(localOemSetting.Key)
					&& serverOemSettings.GetValue(localOemSetting.Key) != localOemSetting.Value)
				{
				}
			}
		}
		private void AddAdvancedPannel(GuiWidget settingsColumn)
		{
			var advancedPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);

			var advancedSection = new SectionWidget("Advanced".Localize(), advancedPanel, theme, serializationKey: "ApplicationSettings-Advanced", expanded: false)
			{
				Name = "Advanced Section",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = 0
			};
			settingsColumn.AddChild(advancedSection);

			theme.ApplyBoxStyle(advancedSection);

			// Touch Screen Mode
			this.AddSettingsRow(
				new SettingsItem(
					"Touch Screen Mode".Localize(),
					theme,
					new SettingsItem.ToggleSwitchConfig()
					{
						Checked = UserSettings.Instance.get(UserSettingsKey.ApplicationDisplayMode) == "touchscreen",
						ToggleAction = (itemChecked) =>
						{
							string displayMode = itemChecked ? "touchscreen" : "responsive";
							if (displayMode != UserSettings.Instance.get(UserSettingsKey.ApplicationDisplayMode))
							{
								UserSettings.Instance.set(UserSettingsKey.ApplicationDisplayMode, displayMode);
								UiThread.RunOnIdle(() => ApplicationController.Instance.ReloadAll().ConfigureAwait(false));
							}
						}
					}),
				advancedPanel);

			AddUserBoolToggle(advancedPanel,
				"Utilize High Res Monitors".Localize(),
				UserSettingsKey.ApplicationUseHeigResDisplays,
				true,
				false);

			AddUserBoolToggle(advancedPanel,
				"Enable Socketeer Client".Localize(),
				UserSettingsKey.ApplicationUseSocketeer,
				true,
				false);

			var openCacheButton = new IconButton(AggContext.StaticData.LoadIcon("fa-link_16.png", 16, 16, theme.InvertIcons), theme)
			{
				ToolTipText = "Open Folder".Localize(),
			};
			openCacheButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				Process.Start(ApplicationDataStorage.ApplicationUserDataPath);
			});

			this.AddSettingsRow(
				new SettingsItem(
					"Application Storage".Localize(),
					openCacheButton,
					theme),
				advancedPanel);

			var clearCacheButton = new HoverIconButton(AggContext.StaticData.LoadIcon("remove.png", 16, 16, theme.InvertIcons), theme)
			{
				ToolTipText = "Clear Cache".Localize(),
			};
			clearCacheButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				CacheDirectory.DeleteCacheData();
			});

			this.AddSettingsRow(
				new SettingsItem(
					"Application Cache".Localize(),
					clearCacheButton,
					theme),
				advancedPanel);

#if DEBUG
			var configureIcon = AggContext.StaticData.LoadIcon("fa-cog_16.png", 16, 16, theme.InvertIcons);

			var configurePluginsButton = new IconButton(configureIcon, theme)
			{
				ToolTipText = "Configure Plugins".Localize(),
				Margin = 0
			};
			configurePluginsButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					DialogWindow.Show<PluginsPage>();
				});
			};

			this.AddSettingsRow(
				new SettingsItem(
					"Plugins".Localize(),
					configurePluginsButton,
					theme),
				advancedPanel);
#endif

			advancedPanel.Children<SettingsItem>().First().Border = new BorderDouble(0, 1);
		}

		private void AddUsserOptionsPannel(GuiWidget settingsColumn)
		{
			var optionsPanel = new FlowLayoutWidget(FlowDirection.TopToBottom);

			var optionsSection = new SectionWidget("Options".Localize(), optionsPanel, theme, serializationKey: "ApplicationSettings-Options", expanded: false)
			{
				Name = "Options Section",
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Margin = 0
			};
			settingsColumn.AddChild(optionsSection);

			theme.ApplyBoxStyle(optionsSection);

			AddUserBoolToggle(optionsPanel,
				"Shown Welcome Message".Localize(),
				UserSettingsKey.ShownWelcomeMessage,
				false,
				false);

			AddUserBoolToggle(optionsPanel,
				"Shown Print Canceled Message".Localize(),
				UserSettingsKey.ShownPrintCanceledMessage,
				false,
				false);

			AddUserBoolToggle(optionsPanel,
				"Shown Print Complete Message".Localize(),
				UserSettingsKey.ShownPrintCompleteMessage,
				false,
				false);

			optionsPanel.Children<SettingsItem>().First().Border = new BorderDouble(0, 1);
		}

		private void AddUserBoolToggle(FlowLayoutWidget advancedPanel, string title, string boolKey, bool requiresRestart, bool reloadAll)
		{
			this.AddSettingsRow(
				new SettingsItem(
					title,
					theme,
					new SettingsItem.ToggleSwitchConfig()
					{
						Checked = UserSettings.Instance.get(boolKey) != "false",
						ToggleAction = (itemChecked) =>
						{
							string boolValue = itemChecked ? "true" : "false";
							if (boolValue != UserSettings.Instance.get(boolKey))
							{
								UserSettings.Instance.set(boolKey, boolValue);
								if (requiresRestart)
								{
									StyledMessageBox.ShowMessageBox(
										"To finish changing your monitor settings you need to restart MatterControl. If after changing your fonts are too small you can adjust Text Size.".Localize(),
										"Restart Required".Localize());
								}

								if (reloadAll)
								{
									UiThread.RunOnIdle(() => ApplicationController.Instance.ReloadAll().ConfigureAwait(false));
								}
							}
						}
					}),
				advancedPanel);
		}

		private void AddApplicationBoolToggle(FlowLayoutWidget advancedPanel, string title, string boolKey, bool requiresRestart, bool reloadAll)
		{
			this.AddSettingsRow(
				new SettingsItem(
					title,
					theme,
					new SettingsItem.ToggleSwitchConfig()
					{
						Checked = ApplicationSettings.Instance.get(boolKey) == "true",
						ToggleAction = (itemChecked) =>
						{
							string boolValue = itemChecked ? "true" : "false";
							if (boolValue != UserSettings.Instance.get(boolKey))
							{
								ApplicationSettings.Instance.set(boolKey, boolValue);
								if (requiresRestart)
								{
									StyledMessageBox.ShowMessageBox(
										"To finish changing your monitor settings you need to restart MatterControl. If after changing your fonts are too small you can adjust Text Size.".Localize(),
										"Restart Required".Localize());
								}

								if (reloadAll)
								{
									UiThread.RunOnIdle(() => ApplicationController.Instance.ReloadAll().ConfigureAwait(false));
								}
							}
						}
					}),
				advancedPanel);
		}

		private void AddSettingsRow(GuiWidget widget, GuiWidget container)
		{
			container.AddChild(widget);
			widget.Padding = widget.Padding.Clone(right: 10);
		}
	}
}
