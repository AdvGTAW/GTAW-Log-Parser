using System;
using System.IO;
using System.Diagnostics;
using Parser.Controllers;
using Parser.Localization;
using System.Windows.Forms;

namespace Parser.UI
{
    public partial class Main : Form
    {
        public Main()
        {
            InitializeComponent();
            LoadSettings();
            SetupServerList();
        }

        private void SetupServerList()
        {
            string currentLanguage = LocalizationController.GetLanguageFromCode(LocalizationController.GetLanguage());
            for (int i = 0; i < ((LocalizationController.Language[])Enum.GetValues(typeof(LocalizationController.Language))).Length; ++i)
            {
                LocalizationController.Language language = (LocalizationController.Language)i;
                ToolStripItem newLanguage = ServerToolStripMenuItem.DropDownItems.Add(language.ToString());
                newLanguage.Click += (sender, args) =>
                {
                    if (((ToolStripMenuItem)newLanguage).Checked)
                        return;
                    if (MessageBox.Show(Strings.SwitchServer, Strings.Restart, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;

                    LocalizationController.SetLanguage(language);
                    ProcessStartInfo startInfo = Process.GetCurrentProcess().StartInfo;
                    startInfo.FileName = Application.ExecutablePath;
                    startInfo.Arguments = ProgramController.ParameterPrefix + "restart";
                    Process.Start(startInfo);
                    Application.Exit();
                };

                if (currentLanguage == language.ToString())
                    ((ToolStripMenuItem)ServerToolStripMenuItem.DropDownItems[i]).Checked = true;
            }
        }

        private void SaveSettings()
        {
            Properties.Settings.Default.RemoveTimestamps = RemoveTimestamps.Checked;
            Properties.Settings.Default.Save();
        }

        private void LoadSettings()
        {
            Version.Text = ProgramController.Version;
            RemoveTimestamps.Checked = Properties.Settings.Default.RemoveTimestamps;

            if (Properties.Settings.Default.FirstStart)
            {
                Properties.Settings.Default.FirstStart = false;
                Properties.Settings.Default.Save();
            }
        }

        private void Parse_Click(object sender, EventArgs e)
        {
            Parsed.Text = ProgramController.ParseChatLog(RemoveTimestamps.Checked);
        }

        private void SaveParsed_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Parsed.Text))
                return;

            try
            {
                SaveFileDialog.FileName = "chatlog.txt";
                SaveFileDialog.Filter = @"Text File | *.txt";
                if (SaveFileDialog.ShowDialog() != DialogResult.OK)
                    return;

                using (StreamWriter writer = new StreamWriter(SaveFileDialog.OpenFile()))
                    writer.Write(Parsed.Text.Replace("\n", Environment.NewLine));
            }
            catch
            {
                MessageBox.Show(Strings.SaveError, Strings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CopyParsedToClipboard_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(Parsed.Text))
                Clipboard.SetText(Parsed.Text.Replace("\n", Environment.NewLine));
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSettings();
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "GTA World Parser Mini FiveM " + ProgramController.Version + Environment.NewLine + "Manual snapshot of the visible FiveM GTAW chat.",
                Strings.Information,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
