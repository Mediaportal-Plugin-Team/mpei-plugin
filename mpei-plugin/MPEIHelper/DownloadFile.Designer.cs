namespace MPEIHelper
{
    partial class DownloadFile
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
          this.progressBar = new System.Windows.Forms.ProgressBar();
          this.btnCancel = new System.Windows.Forms.Button();
          this.lblProgress = new System.Windows.Forms.Label();
          this.SuspendLayout();
          // 
          // progressBar1
          // 
          this.progressBar.Location = new System.Drawing.Point(12, 24);
          this.progressBar.Name = "progressBar1";
          this.progressBar.Size = new System.Drawing.Size(496, 23);
          this.progressBar.TabIndex = 0;
          // 
          // button1
          // 
          this.btnCancel.Location = new System.Drawing.Point(218, 57);
          this.btnCancel.Name = "button1";
          this.btnCancel.Size = new System.Drawing.Size(75, 23);
          this.btnCancel.TabIndex = 1;
          this.btnCancel.Text = "Cancel";
          this.btnCancel.UseVisualStyleBackColor = true;
          this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
          // 
          // label1
          // 
          this.lblProgress.AutoSize = true;
          this.lblProgress.Location = new System.Drawing.Point(12, 6);
          this.lblProgress.Name = "label1";
          this.lblProgress.Size = new System.Drawing.Size(107, 13);
          this.lblProgress.TabIndex = 2;
          this.lblProgress.Text = "Download starting ....";
          // 
          // DownloadFile
          // 
          this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
          this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
          this.AutoSize = true;
          this.ClientSize = new System.Drawing.Size(520, 87);
          this.ControlBox = false;
          this.Controls.Add(this.lblProgress);
          this.Controls.Add(this.btnCancel);
          this.Controls.Add(this.progressBar);
          this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
          this.MaximizeBox = false;
          this.MinimizeBox = false;
          this.Name = "DownloadFile";
          this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
          this.Text = "Download";
          this.Shown += new System.EventHandler(this.DownloadFile_Shown);
          this.ResumeLayout(false);
          this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblProgress;
    }
}