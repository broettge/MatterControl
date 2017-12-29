﻿/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class CheckboxField : UIField
	{
		private CheckBox checkBoxWidget;

		public override void Initialize(int tabIndex)
		{
			checkBoxWidget = new CheckBox("")
			{
				VAnchor = VAnchor.Bottom,
				Name = this.Name,
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Checked = this.Value == "1"
			};
			checkBoxWidget.CheckedStateChanged += (s, e) =>
			{
				this.SetValue(
					checkBoxWidget.Checked ? "1" : "0",
					userInitiated: true);
			};

			this.Content = checkBoxWidget;
		}

		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			checkBoxWidget.Checked = this.Value == "1";
			base.OnValueChanged(fieldChangedEventArgs);
		}
	}


	public class ToggleboxField : UIField
	{
		private CheckBox checkBoxWidget;
		private Color textColor;

		public ToggleboxField(Color textColor)
		{
			this.textColor = textColor;
		}

		public override void Initialize(int tabIndex)
		{
			var pixelWidth = this.ControlWidth + 6; // HACK: work around agg-bug where text fields are padding*2 bigger than ControlWidth

			checkBoxWidget = ImageButtonFactory.CreateToggleSwitch(false, textColor, pixelWidth, pixelHeight: 24 * GuiWidget.DeviceScale, useStandardLabels: false);
			checkBoxWidget.VAnchor = VAnchor.Center;
			checkBoxWidget.Name = this.Name;
			checkBoxWidget.Margin = 0;
			checkBoxWidget.Click += (s, e) =>
			{
				Console.WriteLine("Checkbox Click Event: " + this.checkBoxWidget.Checked);

				this.SetValue(
					this.checkBoxWidget.Checked ? "1" : "0",
					userInitiated: true);
			};

			this.Content = this.checkBoxWidget;
		}

		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			checkBoxWidget.Checked = this.Value == "1";
			base.OnValueChanged(fieldChangedEventArgs);
		}
	}
}
