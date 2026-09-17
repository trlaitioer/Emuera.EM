#nullable enable
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace MinorShift.Emuera.Forms;

internal sealed partial class DebugConfigDialog : Form
{
	public DebugConfigDialog()
	{
		InitializeComponent();

		numericUpDownDWW.Maximum = 10000;
		numericUpDownDWH.Maximum = 10000;
		numericUpDownDWX.Maximum = 10000;
		numericUpDownDWY.Maximum = 10000;
	}

	public void TranslateUI()
	{
		Text = Lang.UI.DebugConfigDialog.Text;
		tabPageDebug3.Text = Lang.UI.DebugConfigDialog.Name.Text;
		label29.Text = Lang.UI.DebugConfigDialog.Warning.Text;
		checkBoxShowDW.Text = Lang.UI.DebugConfigDialog.OpenDebugWindowOnStartup.Text;
		checkBoxDWTM.Text = Lang.UI.DebugConfigDialog.AlwaysOnTop.Text;
		label28.Text = Lang.UI.DebugConfigDialog.WindowWidth.Text;
		label27.Text = Lang.UI.DebugConfigDialog.WindowHeight.Text;
		button6.Text = Lang.UI.ConfigDialog.Window.GetWindowSize.Text;
		checkBoxSetDWPos.Text = Lang.UI.DebugConfigDialog.SetWindowPos.Text;
		label26.Text = Lang.UI.DebugConfigDialog.WindowX.Text;
		label25.Text = Lang.UI.DebugConfigDialog.WindowY.Text;
		button5.Text = Lang.UI.ConfigDialog.Window.GetWindowPos.Text;
		label16.Text = Lang.UI.ConfigDialog.ChangeWontTakeEffectUntilRestart.Text;
		buttonSave.Text = Lang.UI.ConfigDialog.Save.Text;
		buttonCancel.Text = Lang.UI.ConfigDialog.Cancel.Text;


		Size diff = tabControl.Size - tabControl.DisplayRectangle.Size + (Size)tabControl.Padding;
		Size size;
		{
			FlowLayoutPanel page = flowLayoutPanel5;
			page.Dock = DockStyle.None;
			size = page.Size + page.Margin.Size;
			page.Dock = DockStyle.Fill;
		}
		tabControl.Size = size + diff;
	}

	private void buttonSave_Click(object sender, EventArgs e)
	{
		SaveConfig();
		Result = ConfigDialogResult.Save;
		Close();
	}

	private void buttonCancel_Click(object sender, EventArgs e)
	{
		Result = ConfigDialogResult.Cancel;
		Close();
	}

	public ConfigDialogResult Result = ConfigDialogResult.Cancel;

	private static void setCheckBox(CheckBox checkbox, ConfigCode code)
	{
		ConfigItem<bool> item = ConfigData.Instance.GetRequiredDebugItem<bool>(code);
		checkbox.Checked = item.Value;
		checkbox.Enabled = !item.Fixed;
	}

	private static void setNumericUpDown(NumericUpDown updown, ConfigCode code)
	{
		ConfigItem<int> item = ConfigData.Instance.GetRequiredDebugItem<int>(code);
		decimal value = item.Value;
		if (updown.Maximum < value)
		{
			updown.Maximum = value;
		}

		if (updown.Minimum > value)
		{
			updown.Minimum = value;
		}

		updown.Value = value;
		updown.Enabled = !item.Fixed;
	}

	public void SetConfig(DebugDialog debugDialog)
	{
		dd = debugDialog;
		//ConfigData config = ConfigData.Instance;

		setCheckBox(checkBoxShowDW, ConfigCode.DebugShowWindow);
		setCheckBox(checkBoxDWTM, ConfigCode.DebugWindowTopMost);
		setCheckBox(checkBoxSetDWPos, ConfigCode.DebugSetWindowPos);
		setNumericUpDown(numericUpDownDWW, ConfigCode.DebugWindowWidth);
		setNumericUpDown(numericUpDownDWH, ConfigCode.DebugWindowHeight);
		setNumericUpDown(numericUpDownDWX, ConfigCode.DebugWindowPosX);
		setNumericUpDown(numericUpDownDWY, ConfigCode.DebugWindowPosY);
	}

	private void SaveConfig()
	{
		//ConfigData config = ConfigData.Instance.Copy();
		ConfigData config = ConfigData.Instance;
		config.GetRequiredDebugItem(ConfigCode.DebugShowWindow).SetValue(checkBoxShowDW.Checked);
		config.GetRequiredDebugItem(ConfigCode.DebugWindowTopMost).SetValue(checkBoxDWTM.Checked);
		config.GetRequiredDebugItem(ConfigCode.DebugSetWindowPos).SetValue(checkBoxSetDWPos.Checked);
		config.GetRequiredDebugItem(ConfigCode.DebugWindowWidth).SetValue((int)numericUpDownDWW.Value);
		config.GetRequiredDebugItem(ConfigCode.DebugWindowHeight).SetValue((int)numericUpDownDWH.Value);
		config.GetRequiredDebugItem(ConfigCode.DebugWindowPosX).SetValue((int)numericUpDownDWX.Value);
		config.GetRequiredDebugItem(ConfigCode.DebugWindowPosY).SetValue((int)numericUpDownDWY.Value);
		config.SaveDebugConfig();
	}


	private void comboBoxReduceArgumentOnLoad_SelectedIndexChanged(object sender, EventArgs e)
	{ }

	// Assigned by SetConfig(DebugDialog) before the dialog is shown.
	private DebugDialog dd = null!;

	private void button6_Click(object sender, EventArgs e)
	{
		if (dd == null || !dd.Created)
		{
			return;
		}

		if (numericUpDownDWW.Enabled)
		{
			numericUpDownDWW.Value = dd.Width;
		}

		if (numericUpDownDWH.Enabled)
		{
			numericUpDownDWH.Value = dd.Height;
		}
	}

	private void button5_Click(object sender, EventArgs e)
	{
		if (dd == null || !dd.Created)
		{
			return;
		}

		if (numericUpDownDWX.Enabled)
		{
			if (numericUpDownDWX.Maximum < dd.Location.X)
			{
				numericUpDownDWX.Maximum = dd.Location.X;
			}

			if (numericUpDownDWX.Minimum > dd.Location.X)
			{
				numericUpDownDWX.Minimum = dd.Location.X;
			}

			numericUpDownDWX.Value = dd.Location.X;
		}

		if (numericUpDownDWY.Enabled)
		{
			if (numericUpDownDWY.Maximum < dd.Location.Y)
			{
				numericUpDownDWY.Maximum = dd.Location.Y;
			}

			if (numericUpDownDWY.Minimum > dd.Location.Y)
			{
				numericUpDownDWY.Minimum = dd.Location.Y;
			}

			numericUpDownDWY.Value = dd.Location.Y;
		}
	}
}