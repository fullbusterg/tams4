﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace tams4a.Controls
{
    public partial class Panel_Module : UserControl
    {
        private EventHandler Handler { get; set; }

        public Panel_Module()
        {
            InitializeComponent();
        }

        public void setChangedHandler(EventHandler handler)
        {
            Handler = handler;
        }

        public void moduleValueChanged(object sender, EventArgs e)
        {
            Handler(sender, e);
        }
    }
}