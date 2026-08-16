namespace Parser.UI
{
    partial class Main
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.Parsed = new System.Windows.Forms.RichTextBox();
            this.CopyParsedToClipboard = new System.Windows.Forms.Button();
            this.SaveParsed = new System.Windows.Forms.Button();
            this.Parse = new System.Windows.Forms.Button();
            this.SaveFileDialog = new System.Windows.Forms.SaveFileDialog();
            this.Version = new System.Windows.Forms.Label();
            this.RemoveTimestamps = new System.Windows.Forms.CheckBox();
            this.MenuStrip = new System.Windows.Forms.MenuStrip();
            this.ServerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.AboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.MenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // Parsed
            // 
            this.Parsed.DetectUrls = false;
            this.Parsed.Location = new System.Drawing.Point(18, 46);
            this.Parsed.Name = "Parsed";
            this.Parsed.Size = new System.Drawing.Size(375, 158);
            this.Parsed.TabIndex = 0;
            this.Parsed.Text = "";
            // 
            // CopyParsedToClipboard
            // 
            this.CopyParsedToClipboard.Location = new System.Drawing.Point(222, 220);
            this.CopyParsedToClipboard.Name = "CopyParsedToClipboard";
            this.CopyParsedToClipboard.Size = new System.Drawing.Size(171, 25);
            this.CopyParsedToClipboard.TabIndex = 4;
            this.CopyParsedToClipboard.Text = "Copy to Clipboard";
            this.CopyParsedToClipboard.UseVisualStyleBackColor = true;
            this.CopyParsedToClipboard.Click += new System.EventHandler(this.CopyParsedToClipboard_Click);
            // 
            // SaveParsed
            // 
            this.SaveParsed.Location = new System.Drawing.Point(118, 220);
            this.SaveParsed.Name = "SaveParsed";
            this.SaveParsed.Size = new System.Drawing.Size(98, 25);
            this.SaveParsed.TabIndex = 3;
            this.SaveParsed.Text = "Save As";
            this.SaveParsed.UseVisualStyleBackColor = true;
            this.SaveParsed.Click += new System.EventHandler(this.SaveParsed_Click);
            // 
            // Parse
            // 
            this.Parse.Location = new System.Drawing.Point(18, 220);
            this.Parse.Name = "Parse";
            this.Parse.Size = new System.Drawing.Size(94, 25);
            this.Parse.TabIndex = 2;
            this.Parse.Text = "Parse";
            this.Parse.UseVisualStyleBackColor = true;
            this.Parse.Click += new System.EventHandler(this.Parse_Click);
            // 
            // Version
            // 
            this.Version.AutoSize = true;
            this.Version.Location = new System.Drawing.Point(344, 30);
            this.Version.Name = "Version";
            this.Version.Size = new System.Drawing.Size(35, 15);
            this.Version.TabIndex = 5;
            this.Version.Text = "v0.0.0";
            // 
            // RemoveTimestamps
            // 
            this.RemoveTimestamps.AutoSize = true;
            this.RemoveTimestamps.Location = new System.Drawing.Point(18, 259);
            this.RemoveTimestamps.Name = "RemoveTimestamps";
            this.RemoveTimestamps.Size = new System.Drawing.Size(127, 19);
            this.RemoveTimestamps.TabIndex = 6;
            this.RemoveTimestamps.Text = "Remove timestamps";
            this.RemoveTimestamps.UseVisualStyleBackColor = true;
            // 
            // MenuStrip
            // 
            this.MenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ServerToolStripMenuItem,
            this.AboutToolStripMenuItem});
            this.MenuStrip.Location = new System.Drawing.Point(0, 0);
            this.MenuStrip.Name = "MenuStrip";
            this.MenuStrip.Size = new System.Drawing.Size(411, 24);
            this.MenuStrip.TabIndex = 7;
            // 
            // ServerToolStripMenuItem
            // 
            this.ServerToolStripMenuItem.Name = "ServerToolStripMenuItem";
            this.ServerToolStripMenuItem.Size = new System.Drawing.Size(55, 20);
            this.ServerToolStripMenuItem.Text = "Server";
            this.ServerToolStripMenuItem.Visible = false;
            // 
            // AboutToolStripMenuItem
            // 
            this.AboutToolStripMenuItem.Name = "AboutToolStripMenuItem";
            this.AboutToolStripMenuItem.Size = new System.Drawing.Size(52, 20);
            this.AboutToolStripMenuItem.Text = "About";
            this.AboutToolStripMenuItem.Click += new System.EventHandler(this.AboutToolStripMenuItem_Click);
            // 
            // Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(411, 291);
            this.Controls.Add(this.RemoveTimestamps);
            this.Controls.Add(this.Version);
            this.Controls.Add(this.Parse);
            this.Controls.Add(this.SaveParsed);
            this.Controls.Add(this.CopyParsedToClipboard);
            this.Controls.Add(this.Parsed);
            this.Controls.Add(this.MenuStrip);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MainMenuStrip = this.MenuStrip;
            this.MaximizeBox = false;
            this.Name = "Main";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "GTA World Parser Mini FiveM";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Main_FormClosing);
            this.MenuStrip.ResumeLayout(false);
            this.MenuStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.RichTextBox Parsed;
        private System.Windows.Forms.Button CopyParsedToClipboard;
        private System.Windows.Forms.Button SaveParsed;
        private System.Windows.Forms.Button Parse;
        private System.Windows.Forms.SaveFileDialog SaveFileDialog;
        private System.Windows.Forms.Label Version;
        private System.Windows.Forms.CheckBox RemoveTimestamps;
        private System.Windows.Forms.MenuStrip MenuStrip;
        private System.Windows.Forms.ToolStripMenuItem ServerToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem AboutToolStripMenuItem;
    }
}
