﻿namespace tams4a.Forms
{
    partial class FormPicture
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
            this.buttonOK = new System.Windows.Forms.Button();
            this.pictureRoad = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.pictureRoad)).BeginInit();
            this.SuspendLayout();
            // 
            // buttonOK
            // 
            this.buttonOK.Location = new System.Drawing.Point(357, 552);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(101, 33);
            this.buttonOK.TabIndex = 1;
            this.buttonOK.Text = "OK";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // pictureRoad
            // 
            this.pictureRoad.Location = new System.Drawing.Point(0, 0);
            this.pictureRoad.Name = "pictureRoad";
            this.pictureRoad.Size = new System.Drawing.Size(854, 513);
            this.pictureRoad.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureRoad.TabIndex = 0;
            this.pictureRoad.TabStop = false;
            // 
            // FormPicture
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(850, 597);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.pictureRoad);
            this.Name = "FormPicture";
            this.Text = "FormPicture";
            ((System.ComponentModel.ISupportInitialize)(this.pictureRoad)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button buttonOK;
        public System.Windows.Forms.PictureBox pictureRoad;
    }
}