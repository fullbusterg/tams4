﻿using DotSpatial.Symbology;
using DotSpatial.Controls;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using tams4a.Controls;
using tams4a.Forms;

namespace tams4a.Classes
{
    class ModuleRoads : ProjectModule
    {
        public new const string moduleVersion = "4.0.1.1";    // string that can be converted to System.Version

        private bool colorsOn = true;
        private DataTable surfaceTypes;
        private DataTable roadTypes;
        private DataTable surfaceDistresses;
        private DateTime RecordingDate;
        private string notes;
        private List<string> tamsids;
        private FormSurveyDate dateForm;
        // used to override the base class SelectionSql
        static private readonly string RoadSelectionSql = @"SELECT MAX(roadinfo.id) AS max_id, roadinfo.* 
                    FROM
                    (
                        SELECT TAMSID, MAX(survey_date) AS maxdate
                        FROM road
                        WHERE TAMSID IN ([[IDLIST]])
                        GROUP BY TAMSID
                    ) AS roadids
                    JOIN road AS roadinfo
                        ON (
                                roadinfo.TAMSID = roadids.TAMSID AND
                                roadinfo.survey_date = roadids.maxdate
                            )
                    GROUP BY roadinfo.TAMSID";

        public ModuleRoads(TamsProject theProject, TabPage controlPage, ToolStripMenuItem[] boundButtons) : base(theProject, controlPage, boundButtons, RoadSelectionSql)
        {
            ModuleName = "road";
            RecordingDate = DateTime.Now;   // default value
            notes = "";
            boundButtons[2].Click += potholeReport;
            boundButtons[1].Click += generalReport;
            boundButtons[3].Click += openBudgetTool;
            // Add the control to open a new shpfile.
            Panel_Module_OpenShp roadAdd = new Panel_Module_OpenShp("Road");
            roadAdd.Name = "ROADADD";
            roadAdd.SetHandler(new EventHandler(openFileHandler));
            roadAdd.Dock = DockStyle.Fill;
            ControlsPage.Controls.Add(roadAdd);

            // Settings needed to open the project
            // Other settings will be added when we open the file
            ModuleSettings.Add(new ProjectSetting(name:ModuleName + "_file", module:ModuleName));
            ModuleSettings.Add(new ProjectSetting(name:ModuleName + "_relative", module:ModuleName));

            // gives a map from the setting name (containing the shp field) to the database column name
            FieldSettingToDbColumn = new Dictionary<string, string>()
            {
                { "road_f_TAMSID", "TAMSID" },
                { "road_f_streetname", "name"},
                { "road_f_rsl", "rsl" },
                { "road_f_width", "width" },
                { "road_f_length", "length" },
                { "road_f_speedlimit", "speed_limit" },
                { "road_f_startaddr", "from_address" },
                { "road_f_endaddr", "to_address" },
                { "road_f_surfacetype", "surface" }
            };

            Project.map.ResetBuffer();
            Project.map.Update();
            // TODO: Rest
        }

        // must be type "line" for roads
        public override Boolean openFile(string thePath = "", string type = "line")
        {
            if (type == "") { type = "line";  }
            if (type != "line") { throw new Exception("Roads module requires a line-type shp file"); }

            // add in any further required settings
            #region Additional module settings
            ModuleSettings.Add(new ProjectSetting(name: ModuleName + "_f_TAMSID", module: ModuleName, value: "",
                    display_text: "SHP field with unique identifier.",
                    display_type: "field", required: true));
            ModuleSettings.Add(new ProjectSetting(name: "road_f_streetname", module: ModuleName, value: "",
                    display_text: "SHP Field for road name", display_type: "field",
                    description: "Field in the SHP file for the street name.  e.g. 100 South, Main, Oak Ave."));
            ModuleSettings.Add(new ProjectSetting(name: "road_f_width", module: ModuleName, value: "",
                    display_text: "SHP Field for width (ft)", display_type: "field",
                    description: "Field in the SHP file for the road width."));
            ModuleSettings.Add(new ProjectSetting(name: "road_f_length", module: ModuleName, value: "",
                    display_text: "SHP Field for length (ft)", display_type: "field",
                    description: "Field in the SHP file for segment length."));
            ModuleSettings.Add(new ProjectSetting(name: "road_f_speedlimit", module: ModuleName, value: "",
                    display_text: "SHP Field for speed limit", display_type: "field",
                    description: "Field in the SHP file for speed limit."));
            ModuleSettings.Add(new ProjectSetting(name: "road_f_startaddr", module: ModuleName, value: "",
                    display_text: "SHP Field for starting address number", display_type: "field",
                    description: "Field in the SHP file for starting address number."));
            ModuleSettings.Add(new ProjectSetting(name: "road_f_endaddr", module: ModuleName, value: "",
                    display_text: "SHP Field for ending address number", display_type: "field",
                    description: "Field in the SHP file for ending address number."));
            ModuleSettings.Add(new ProjectSetting(name: "road_f_surfacetype", module: ModuleName, value: "",
                    display_text: "SHP Field for road surface", display_type: "field",
                    description: "Field in the SHP file for the pavement used by the road, e.g. asphalt."));
            ModuleSettings.Add(new ProjectSetting(name: "road_labels", module: ModuleName, value: "true",
                    display_text: "Show Labels?", display_type: "bool",
                    description: "Showing street labels (names) may slow down the display."));
            ModuleSettings.Add(new ProjectSetting(name: "road_colours", module: ModuleName, value: "true",
                    display_text: "Use Colors?", display_type: "bool",
                    description: "Color the streets based on observed RSL."));
            //ModuleSettings.Add(new ProjectSetting(name: "road_f_rsl", module: ModuleName, value: "true",
            //        display_text: "SHP Field that holds observed RSL", display_type: "field",
            //        description: "Field in the SHP file RSL."));

            #endregion
            injectSettings();       // add these new settings to the project settings.

            if (!base.openFile(thePath, type)) { return false; }

            // don't need to remove the old event handler because it was registered in this class
            ControlsPage.Controls.Remove(ControlsPage.Controls["ROADADD"]);
            Panel_Road roadPanel = new Panel_Road();
            roadPanel.Name = "ROADCONTROLS";
            roadPanel.Dock = DockStyle.Fill;
            ControlsPage.Controls.Add(roadPanel);
            disableRoadDisplay();   // disabled until something is selected

            // set event handlers
            #region eventhandlers
            roadPanel.buttonSave.Click += saveHandler;
            roadPanel.buttonReset.Click += selectionChanged;    // just pretend we re-selected it
            roadPanel.pictureBoxPhoto.Click += clickPhotoBox;
            roadPanel.toolStripButtonAnalysis.Click += reportSelected;

            roadPanel.btnNotes.Click += editNotes;

            roadPanel.comboBoxSurface.SelectionChangeCommitted += surfaceChanged;
            roadPanel.setChangedHandler(controlChanged);

            roadPanel.distress1.ValueChanged += distressChanged;
            roadPanel.distress2.ValueChanged += distressChanged;
            roadPanel.distress3.ValueChanged += distressChanged;
            roadPanel.distress4.ValueChanged += distressChanged;
            roadPanel.distress5.ValueChanged += distressChanged;
            roadPanel.distress6.ValueChanged += distressChanged;
            roadPanel.distress7.ValueChanged += distressChanged;
            roadPanel.distress8.ValueChanged += distressChanged;
            roadPanel.distress9.ValueChanged += distressChanged;

            roadPanel.buttonHistory.Click += showHistory;
            roadPanel.setOtherDateToolStripMenuItem.Click += selectRecordDate;
            roadPanel.setTodayToolStripMenuItem.Click += resetRecordDate;
            dateForm = new FormSurveyDate();
            dateForm.Hide();
            dateForm.buttonConfirm.Click += setDate;

            #endregion eventhandlers

            #region road controls settings
            // load up surface types
            surfaceTypes = Database.GetDataByQuery(Project.conn, "SELECT * FROM road_surfaces");
            // functional technique for generating combobox options from data table
            DataRow blankSurfaceRow = surfaceTypes.NewRow();    //
            blankSurfaceRow["id"] = 0;                          // add empty row
            blankSurfaceRow["name"] = "";                       //
            surfaceTypes.Rows.InsertAt(blankSurfaceRow, 0);         //
            roadPanel.comboBoxSurface.DataSource = surfaceTypes;    //
            roadPanel.comboBoxSurface.DisplayMember = "name";       // sets options
            roadPanel.comboBoxSurface.ValueMember = "id";           //

            // load distress values
            surfaceDistresses = Database.GetDataByQuery(Project.conn, "SELECT rd.*, rs.name AS surface FROM road_distresses AS rd JOIN road_surfaces AS rs ON rd.surface_id = rs.id ORDER BY rd.id");
            DataColumn[] keys = new DataColumn[1];
            keys[0] = surfaceDistresses.Columns["id"];
            surfaceDistresses.PrimaryKey = keys;

            // load road types (classification)
            roadTypes = Database.GetDataByQuery(Project.conn, "SELECT * FROM road_types");
            // procedural technique for generating combobox options from datatable
            roadPanel.comboBoxType.Items.Add(""); // add empty row
            foreach (DataRow row in roadTypes.Rows)                         //
            {                                                               //
                roadPanel.comboBoxType.Items.Add(row["name"].ToString());   // sets options
            }                                                               //
            #endregion

            applyColorizedProperties();
            setSymbolizer();
            disableRoadDisplay();
            return true;
        }

        private void setDate(object sender, EventArgs args)
        {
            RecordingDate = dateForm.getDate();
        }

        /// <summary>
        /// When map selection changes, set enabled controls, update display, etc
        /// @TO-DO determine if this layer is active layer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public override void selectionChanged(object sender, EventArgs e)
        {
            if (!isOpen()) { return; }

            if (UnsavedChanges)
            {
                DialogResult rslt = MessageBox.Show("Unsaved Changes Detected! Would you like to save the changes? Otherwise, they will be discared", "Unsaved Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (rslt == DialogResult.Yes)
                {
                    saveHandler(sender, e);
                }
                
            }
            resetRoadDisplay();

            FeatureLayer selectionLayer = (FeatureLayer)Layer;
            ISelection shpSelection = selectionLayer.Selection;
            Dictionary<string, string> values = new Dictionary<string, string>();
            
            // If they clicked on a blank spot, we're done.
            if (shpSelection.Count <= 0) {
                disableRoadDisplay();
                return;
            }

            enableControls();   // must have something selected, so enable controls
            values = setSegmentValues(selectionLayer.Selection.ToFeatureSet().DataTable);
            
            updateRoadDisplay(values);

            if (values.ContainsKey("TAMSID") && !string.IsNullOrWhiteSpace(values["TAMSID"]))
            {
                IdText = values["TAMSID"];
            } else
            {
                IdText = "";
            }

            Panel_Road roadControls = getRoadControls();
            //Control roadNameLabel = ControlsPage.Controls.Find("labelName", true).FirstOrDefault() as Control;
            if (shpSelection.Count > 1)  
            {
                // Change color to indicate multiple selection
                roadControls.labelName.ForeColor = SystemColors.HighlightText;
                roadControls.labelName.BackColor = SystemColors.Highlight;
                roadControls.labelName.Text = "Multiple";
                roadControls.textBoxRoadName.Enabled = false;
                roadControls.textBoxRoadName.Text = "";

                // TODO: Set tooltip
            }
            

            string tamsidcolumn = Project.settings.GetValue(ModuleName + "_f_TAMSID");
            tamsids = new List<string>();
            foreach (DataRow row in selectionLayer.Selection.ToFeatureSet().DataTable.Rows)
            {
                tamsids.Add(row[tamsidcolumn].ToString());
            }
        }


        // returns the ROADCONTROLS collection of controls.
        // does not include the toolstrip
        private Panel_Road getRoadControls()
        {
            Panel_Road controls;

            try
            {
                controls = (Panel_Road)ControlsPage.Controls["ROADCONTROLS"];
            }
            catch (Exception e)
            {
                Log.Error("Could not retrieve controls page.\n" + e.ToString());
                throw new Exception("Could not retrieve controls page.\n" + e.ToString());
            }
            return controls;
        }


        // Sets the values of the various controls
        private void updateRoadDisplay(Dictionary<string, string> values)
        {
            Panel_Road roadControls = getRoadControls();

            roadControls.textBoxRoadName.Text = Util.DictionaryItemString(values, "name");
            roadControls.labelSurvDate.Text = "As of " + Util.DictionaryItemString(values, "survey_date");
            roadControls.numericUpDownSpeedLimit.Value = Util.ToInt(Util.DictionaryItemString(values, "speed_limit"));
            roadControls.numericUpDownLanes.Value = Util.ToInt(Util.DictionaryItemString(values, "lanes"));
            roadControls.textBoxFrom.Text = Util.DictionaryItemString(values, "from_address");
            roadControls.textBoxTo.Text = Util.DictionaryItemString(values, "to_address");
            roadControls.textBoxWidth.Text = Util.DictionaryItemString(values, "width"); 
            roadControls.textBoxLength.Text = Util.DictionaryItemString(values, "length"); 

            roadControls.comboBoxType.Text = Util.DictionaryItemString(values, "type");
            roadControls.comboBoxSurface.Text = Util.DictionaryItemString(values, "surface");

            roadControls.textBoxPhotoFile.Text = Util.DictionaryItemString(values, "photo");
            roadControls.toolTip.SetToolTip(roadControls.pictureBoxPhoto, "");
            updatePhotoPreview();

            // distress controls
            if (string.IsNullOrWhiteSpace(roadControls.comboBoxSurface.Text))
            {
                roadControls.groupBoxDistress.Enabled = false;
            } else {
                roadControls.groupBoxDistress.Enabled = true;
                // Update the visible distresses based on surface type
                updateDistressControls(roadControls.comboBoxSurface.Text);
            }
            roadControls.distress1.Value = Util.DictionaryItemInt(values, "distress1");
            roadControls.distress2.Value = Util.DictionaryItemInt(values, "distress2");
            roadControls.distress3.Value = Util.DictionaryItemInt(values, "distress3");
            roadControls.distress4.Value = Util.DictionaryItemInt(values, "distress4");
            roadControls.distress5.Value = Util.DictionaryItemInt(values, "distress5");
            roadControls.distress6.Value = Util.DictionaryItemInt(values, "distress6");
            roadControls.distress7.Value = Util.DictionaryItemInt(values, "distress7");
            roadControls.distress8.Value = Util.DictionaryItemInt(values, "distress8");
            roadControls.distress9.Value = Util.DictionaryItemInt(values, "distress9");
            roadControls.comboBoxTreatment.Text = Util.DictionaryItemString(values, "suggested_treatment");
            // we're taking RSL from DB to later allow manual entry
            roadControls.inputRsl.Value = Util.DictionaryItemString(values, "rsl");

            notes = Util.DictionaryItemString(values, "notes");
            if (!string.IsNullOrEmpty(notes))
            {
                roadControls.btnNotes.Checked = true;
            }

            resetSaveCondition();
        }


        // sets the photo preview to whatever the textbox says.
        private void updatePhotoPreview()
        {
            Panel_Road roadControls = getRoadControls();
            if (!string.IsNullOrWhiteSpace(roadControls.textBoxPhotoFile.Text))
            {
                try
                {
                    string imageLocation = Project.projectFolderPath + @"\Photos\" + roadControls.textBoxPhotoFile.Text;
                    if (File.Exists(imageLocation))
                    {
                        roadControls.pictureBoxPhoto.ImageLocation = imageLocation;
                    }
                    else
                    {
                        Log.Warning("Missing image file: " + imageLocation);
                        roadControls.toolTip.SetToolTip(roadControls.pictureBoxPhoto, "Missing: " + imageLocation);
                        throw new Exception("Missing image file");
                    }
                }
                catch
                {
                    // set to error image.
                    roadControls.pictureBoxPhoto.Image = Properties.Resources.error;
                }
            }
            else
            {
                roadControls.pictureBoxPhoto.Image = Properties.Resources.nophoto;
            }
        }


        // Set everything as though no changes have been made
        private void resetSaveCondition()
        {
            UnsavedChanges = false;
            Panel_Road roadControls = getRoadControls();

            // resets the save button since we've just reset the values
            roadControls.buttonSave.Enabled = false;
            roadControls.buttonSave.BackColor = default(Color);
        }


        // sets the various distress controls based on the surface
        private void updateDistressControls(string surface)
        {
           Panel_Road roadControls = getRoadControls();

            // TODO: don't change anything if we already have it set
            // have to change the order here.
            // was causing problems due to order
            //if (roadControls.comboBoxSurface.Text == surface) { return; }

            // hide all controls
            foreach (Control control in roadControls.groupBoxDistress.Controls)
            {
                control.Enabled = false;
                control.Hide();
            }
            roadControls.comboBoxTreatment.Enabled = false;
            roadControls.comboBoxTreatment.Visible = false;
            if (surface == "") { return; }

            roadControls.groupBoxDistress.Enabled = true;
            roadControls.inputRsl.Visible = true;
            roadControls.comboBoxTreatment.Visible = true;
            roadControls.labelSuggestedTreatment.Visible = true;
            roadControls.comboBoxTreatment.Enabled = true;

            foreach (DataRow row in surfaceDistresses.Rows)
            {
                if (row["surface"].ToString() == surface)
                {
                    try
                    {
                        string dbkey = row["dbkey"].ToString();
                        DistressEntry thisEntry = (DistressEntry)roadControls.groupBoxDistress.Controls[dbkey];
                        thisEntry.Label = row["name"].ToString();
                        thisEntry.MaxDistress = Convert.ToInt16(row["max_distress"]);
                        thisEntry.IllustrationName = row["imageName"].ToString();
                        thisEntry.Description = row["description"].ToString();
                        thisEntry.Show();
                        thisEntry.Value = -1;
                        thisEntry.DataId = Convert.ToInt16(row["id"]);
                        thisEntry.Enabled = true;
                    }
                    catch
                    {
                        // ??
                    }
                }
            }

            var treatments = Database.GetDataByQuery(Project.conn, "SELECT id, name FROM treatments WHERE road_applied='" + roadControls.comboBoxSurface.Text + "';");
            DataRow blankSurfaceRow = treatments.NewRow();    //
            blankSurfaceRow["id"] = 0;                          // add empty row
            blankSurfaceRow["name"] = "";                       //
            treatments.Rows.InsertAt(blankSurfaceRow, 0);         //
            roadControls.comboBoxTreatment.DataSource = treatments;    //
            roadControls.comboBoxTreatment.DisplayMember = "name";       // sets options
            roadControls.comboBoxTreatment.ValueMember = "id";           //
            //roadControls.comboBoxTreatment.DropDownWidth = Util.DropDownWidth(roadControls.comboBoxTreatment);
        }


        // clears the road module controls
        private void resetRoadDisplay()
        {
            Panel_Road roadControls = getRoadControls();

            roadControls.textBoxRoadName.Text = "";
            roadControls.labelSurvDate.Text = "";
            roadControls.numericUpDownSpeedLimit.Value = 25;
            roadControls.numericUpDownLanes.Value = 2;
            roadControls.textBoxFrom.Text = "";
            roadControls.textBoxTo.Text = "";
            roadControls.textBoxWidth.Text = "";
            roadControls.textBoxLength.Text = "";
            roadControls.textBoxArea.Text = "";
            roadControls.comboBoxType.Text = "";
            roadControls.comboBoxSurface.Text = "";
            roadControls.textBoxPhotoFile.Text = "";
            roadControls.pictureBoxPhoto.Image = null;
            roadControls.distress1.Value = -1;
            roadControls.distress2.Value = -1;
            roadControls.distress3.Value = -1;
            roadControls.distress4.Value = -1;
            roadControls.distress5.Value = -1;
            roadControls.distress6.Value = -1;
            roadControls.distress7.Value = -1;
            roadControls.distress8.Value = -1;
            roadControls.distress9.Value = -1;
            roadControls.inputRsl.Value = "";
            roadControls.btnNotes.Checked = false;

            roadControls.labelName.ForeColor = default(Color);
            roadControls.labelName.BackColor = default(Color);
            roadControls.textBoxRoadName.Enabled = true;
            roadControls.labelName.Text = "Road";
        }


        // disables controls of the road panel
        private void disableRoadDisplay()
        {
            Panel_Road roadControls = getRoadControls();
            resetSaveCondition();
            roadControls.groupBoxInfo.Enabled = false;
            roadControls.groupBoxDistress.Enabled = false;
            roadControls.toolStrip.Enabled = false;
        }


        // enables controls for when at least 1 segment is selected
        // some controls depend on whether we've selected multiple items, so optional parameter
        private void enableControls(Boolean multiple = false)
        {
            Panel_Road roadControls = getRoadControls();

            if (multiple)
            {
                roadControls.textBoxPhotoFile.Enabled = false;
                roadControls.buttonNextPhoto.Enabled = false;
            } else
            {
                roadControls.textBoxPhotoFile.Enabled = true;
                roadControls.buttonNextPhoto.Enabled = true;
            }

            roadControls.groupBoxInfo.Enabled = true;
            roadControls.groupBoxDistress.Enabled = true;
            roadControls.toolStrip.Enabled = true;
          
        }

        
        // handler for changed controls
        protected override void controlChanged(object sender, EventArgs e)
        {
            Panel_Road roadControls = getRoadControls();

            roadControls.buttonSave.Enabled = true;
            roadControls.buttonSave.BackColor = Color.Red;
            UnsavedChanges = true;

            roadControls.buttonHistory.Enabled = true;
            roadControls.buttonReset.Enabled = true;
        }


        /// <summary>
        /// Event called to save the data as entered.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void saveHandler(object sender, EventArgs e)
        {
            // get list of TAMSIDS
            // TODO: Track this at module level
            FeatureLayer selectionLayer = (FeatureLayer)Layer;
            ISelection shpSelection = selectionLayer.Selection;
            string tamsidcolumn = Project.settings.GetValue(ModuleName + "_f_TAMSID");

            Panel_Road roadControls = getRoadControls();
            Dictionary<string, string> values = new Dictionary<string, string>();
            values["name"] = roadControls.textBoxRoadName.Text;
            values["survey_date"] = Util.SortableDate(RecordingDate);
            values["speed_limit"] = roadControls.numericUpDownSpeedLimit.Value != 0? roadControls.numericUpDownSpeedLimit.Value.ToString(): "";
            values["lanes"] = roadControls.numericUpDownLanes.Value != 0? roadControls.numericUpDownLanes.Value.ToString(): "";
            values["width"] = roadControls.textBoxWidth.Text;
            values["length"] = roadControls.textBoxLength.Text;
            values["from_address"] = roadControls.textBoxFrom.Text;
            values["to_address"] = roadControls.textBoxTo.Text;
            values["type"] = roadControls.comboBoxType.Text;
            values["surface"] = roadControls.comboBoxSurface.Text;
            values["photo"] = roadControls.textBoxPhotoFile.Text;
            values["notes"] = notes;

            if (!string.IsNullOrWhiteSpace(roadControls.textBoxPhotoFile.Text))
            {
                Properties.Settings.Default.lastPhoto = roadControls.textBoxPhotoFile.Text;
            }

            // Distress values                                                                                          //  Asphalt         Unpaved         Concrete
            if (roadControls.distress1.Visible) { values["distress1"] = roadControls.distress1.Value.ToString(); }   //  Fatigue         Potholes       Spalling
            if (roadControls.distress2.Visible) { values["distress2"] = roadControls.distress2.Value.ToString(); }   //  Edge            Rutting        Joint Seals
            if (roadControls.distress3.Visible) { values["distress3"] = roadControls.distress3.Value.ToString(); }   //  Longitudional   X-section      Corners
            if (roadControls.distress4.Visible) { values["distress4"] = roadControls.distress4.Value.ToString(); }   //  Patches         Drainage       Broken
            if (roadControls.distress5.Visible) { values["distress5"] = roadControls.distress5.Value.ToString(); }   //  PotHoles        Dust           Faulting
            if (roadControls.distress6.Visible) { values["distress6"] = roadControls.distress6.Value.ToString(); }   //  Drainage        Aggregate      Longitudinal
            if (roadControls.distress7.Visible) { values["distress7"] = roadControls.distress7.Value.ToString(); }   //  Transverse      Corrugation    Transverse
            if (roadControls.distress8.Visible) { values["distress8"] = roadControls.distress8.Value.ToString(); }   //  Block                          Cracking
            if (roadControls.distress9.Visible) { values["distress9"] = roadControls.distress9.Value.ToString(); }   //  Rutting                        Patches

            if (roadControls.comboBoxTreatment.Visible) { values["suggested_treatment"] = roadControls.comboBoxTreatment.Text; }

            // also need to update the RSL value in the SHP file's temporary field, for display purposes
            if (!string.IsNullOrWhiteSpace(roadControls.inputRsl.Value.ToString())) {
                values["rsl"] = roadControls.inputRsl.Value.ToString();

                string tamsidsCSV = string.Join(",", tamsids.ToArray());
                foreach (DataRow row in selectionLayer.DataSet.DataTable.Select(tamsidcolumn + " IN (" + tamsidsCSV + ")"))
                {
                    row["TAMSROADRSL"] = values["rsl"];
                    row["TAMSTREATMENT"] = values["suggested_treatment"];
                }
            }

            for (int i = 0; i < tamsids.Count; i++)
            {
                values["TAMSID"] = tamsids[i];
                Dictionary<string, string> v = new Dictionary<string, string>();

                foreach (string key in values.Keys)
                {
                    v[key] = values[key];
                    if ((string.IsNullOrWhiteSpace(values[key]) || values[key].Equals("multi") || values[key].Equals("-1")) && selectionValues.Count > 1)
                    {
                        if (i < selectionValues.Count && selectionValues[i].ContainsKey(key))
                        {
                            v[key] = selectionValues[i][key];
                        }
                    }
                }

                if (!Database.InsertRow(Project.conn, v, "road"))
                {
                    MessageBox.Show("Could not save data!");
                }
            }
            resetSaveCondition();

            updatePhotoPreview();
            Properties.Settings.Default.Save();

            selectionLayer.ClearSelection();
            setSymbolizer();
            Project.map.Invalidate();
            Project.map.Refresh();
            Project.map.ResetBuffer();
            Project.map.Update();
        }


        // Changes contrls to reflect change made to surface type
        private void surfaceChanged(object sender, EventArgs e)
        {
            Panel_Road roadControls = getRoadControls();

            updateDistressControls(roadControls.comboBoxSurface.Text);
            
        }


        // Changes controls to reflect a change made to a distress control
        public void distressChanged(object sender, CustomEventArgs e)
        {
            Panel_Road roadControls = getRoadControls();
            if (roadControls.comboBoxSurface.Text != "")
            {
                // TODO: Ignore if I changed the RSL while blanking the distressEntry control
                roadControls.inputRsl.Value = calcRsl().ToString();

                // change save condition
                controlChanged(sender, e);
            }
        }


        // calculates RSL from values in the (enabled) distress controls
        private int calcRsl()
        {
            Panel_Road roadControls = getRoadControls();
            List<DistressEntry> distresses = new List<DistressEntry>()
            {   roadControls.distress1, roadControls.distress2, roadControls.distress3,
                roadControls.distress4, roadControls.distress5, roadControls.distress6,
                roadControls.distress7, roadControls.distress8, roadControls.distress9
            };

            bool RoadNotSurveyed = true;
            int minrsl = 20; // TODO: get actual minimum from from road surface

            foreach (DistressEntry entry in distresses)
            {
                if (entry.Enabled && entry.Value > 0)       // if = 0, then we don't look it up as it's whatever the default RSL is 
                {
                    RoadNotSurveyed = false;
                    try
                    {
                        string column = "rsl" + entry.Value.ToString();
                        int thisRsl = Convert.ToInt16(surfaceDistresses.Rows.Find(entry.DataId)[column]);

                        if (thisRsl<minrsl)
                        {
                            minrsl = thisRsl;
                        }
                        roadControls.toolTip.SetToolTip(entry, "RSL: " + thisRsl.ToString());
                    }
                    catch
                    {
                        roadControls.toolTip.SetToolTip(entry, "No change to RSL.");

                        // Not all distresses have an RSL value.
                        string distress = entry.Value.ToString();
                        string dbkey = entry.DataId.ToString();
                        string distressName = entry.Name.ToString();
                        string message = "Couldn't find entry #" + distress + " for " + distressName + " (id:" + dbkey + ")";

                        // if we couldn't find it or something, then ignore it
                        //MessageBox.Show(message);   // TODO: Once function works, remove this
                        //Log.Warning(message);
                    }
                }
                else if (entry.Enabled && entry.Value == 0)
                {
                    RoadNotSurveyed = false;
                }
                else
                {
                    roadControls.toolTip.SetToolTip(entry, "");
                }
            }
            if (RoadNotSurveyed)
            {
                return -1;
            }
            return minrsl;
        }

        /// <summary>
        /// Displays the form for adding notes about the road to the database.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void editNotes(object sender, EventArgs e)
        {
            // show notes dialog form
            Forms.FormNotes noteForm = new Forms.FormNotes();
            noteForm.Value = notes;
            DialogResult result = noteForm.ShowDialog();

            if (result == DialogResult.OK)
            {
                Panel_Road roadControls = getRoadControls();
                roadControls.btnNotes.Checked = true;
                controlChanged(sender, e);

                notes = noteForm.Value;
            }
        }


        private void selectRecordDate(object sender, EventArgs e)
        {
            dateForm.Show();
            Panel_Road roadControls = getRoadControls();
            roadControls.setTodayToolStripMenuItem.Checked = false;
            roadControls.setOtherDateToolStripMenuItem.Checked = true;
        }


        private void resetRecordDate(object sender, EventArgs e)
        {
            RecordingDate = DateTime.Now;
            Panel_Road roadControls = getRoadControls();
            roadControls.setTodayToolStripMenuItem.Checked = true;
            roadControls.setOtherDateToolStripMenuItem.Checked = false;
        }

        private void applyColorizedProperties()
        {
            FeatureLayer selectionLayer = (FeatureLayer)Layer;
            selectionLayer.SelectAll();
            ISelection shpSelection = selectionLayer.Selection;
            DataTable selectionTable = shpSelection.ToFeatureSet().DataTable;
            string tamsidcolumn = Project.settings.GetValue(ModuleName + "_f_TAMSID");
            selectionTable.DefaultView.Sort = tamsidcolumn + " asc";
            if (!selectionTable.Columns.Contains("TAMSROADRSL"))
            {
                selectionTable.Columns.Add("TAMSROADRSL");
            }
            if (!selectionTable.Columns.Contains("TAMSTREATMENT"))
            {
                selectionTable.Columns.Add("TAMSTREATMENT");
            }
            string roadSQL = SelectionSql.Replace("[[IDLIST]]", extractTAMSIDs(selectionTable));
            selectionTable = selectionTable.DefaultView.ToTable();
            DataTable tamsTable = Database.GetDataByQuery(Project.conn, roadSQL);
            tamsTable.DefaultView.Sort = "TAMSID asc";
            tamsTable = tamsTable.DefaultView.ToTable();
            for (int i = 0; i < selectionTable.Rows.Count; i++)
            {
                selectionTable.Rows[i]["TAMSROADRSL"] = i >= tamsTable.Rows.Count ? -1: string.IsNullOrWhiteSpace(tamsTable.Rows[i]["rsl"].ToString())? -1: Util.ToInt(tamsTable.Rows[i]["rsl"].ToString());
                selectionTable.Rows[i]["TAMSTREATMENT"] = i >= tamsTable.Rows.Count ? -1: tamsTable.Rows[i]["suggested_treatment"];
            }
            selectionLayer.DataSet.DataTable = selectionTable;
        }

        private void setSymbolizer()
        {
            // These change how the lines look.
            // TODO:  Change these someday to allow for custom look?
            double baseWidth = 20.0;        // for convenience and clarity
            double baseOutlineWidth = 10.0; // ditto
            double adjWidth = baseWidth;    // adjusted width (of lines)
            double adjOutlineWidth = baseOutlineWidth;
            int numcategories = 6;
            int maxrsl = 20;        // TODO:  This should come from the selection type


            LineScheme rdScheme = new LineScheme();

            // category selection symbolizer
            LineSymbolizer catSelSym = new LineSymbolizer();
            catSelSym.ScaleMode = ScaleMode.Geographic;
            catSelSym.SetWidth(adjWidth);
            catSelSym.SetOutline(Color.Blue, adjOutlineWidth);
            catSelSym.SetFillColor(Color.White);

            // symoblizer default
            LineSymbolizer symDef = new LineSymbolizer();
            symDef.ScaleMode = ScaleMode.Geographic;
            symDef.SetWidth(adjWidth);
            symDef.SetOutline(Color.Black, adjOutlineWidth);
            symDef.SetFillColor(Color.Gray);

            // default category
            LineCategory catDef = new LineCategory();
            catDef.LegendText = "No RSL Info";
            
            catDef.SelectionSymbolizer = catSelSym;
            catDef.Symbolizer = symDef;
            rdScheme.AddCategory(catDef);
            
            int[] rslfloor = {0, 1, 5,  9, 13, 17 };
            int[] rslceil = { 0, 4, 8, 12, 16, 20 };
            int[] r = { 255, 240, 250,  90,  5,  35 };
            int[] g = {  5,  130, 250, 200, 255, 100 };
            int[] b = {  5,   5,   5,   30, 10, 255 };
            
            // add rsl categories
            if (Project.settings.GetValue("road_colours").Contains("t"))
            {
                int j = 0;
                for (int i = 0; i < 21; i++)
                {
                    while (i > rslceil[j])
                    {
                        j++;
                    }
                    // create a category
                    LineCategory colourCat = new LineCategory();
                    colourCat.FilterExpression = "[TAMSROADRSL] = '" + i.ToString() + "'";

                    // create a new symbolizer, then assign it to category
                    //LineSymbolizer sym = new LineSymbolizer(Color.FromArgb(r, g, b), Color.Black, adjWidth, System.Drawing.Drawing2D.DashStyle.Solid,
                    //        System.Drawing.Drawing2D.LineCap.Flat);
                    //sym.ScaleMode = ScaleMode.Geographic;
                    //sym.SetOutline(Color.Red, adjOutlineWidth);
                    LineSymbolizer colourSym = new LineSymbolizer();
                    colourSym.ScaleMode = ScaleMode.Geographic;
                    colourSym.SetWidth(adjWidth);
                    colourSym.SetOutline(Color.DarkGray, adjOutlineWidth);
                    colourSym.SetFillColor(Color.FromArgb(r[j], g[j], b[j]));

                    colourCat.Symbolizer = colourSym;

                    // assign (default) selection symbolizer
                    colourCat.SelectionSymbolizer = catSelSym;

                    // in case we ever use the legend
                    //cat.LegendText = "RSL " + reader["start"].ToString() + "-" + reader["end"].ToString();
                    colourCat.LegendText = "RSL " + rslfloor.ToString() + " +";

                    // done
                    rdScheme.AddCategory(colourCat);
                }
            }

            LineCategory defCat = new LineCategory();
            defCat.FilterExpression = "[TAMSROADRSL] = -1";

            LineSymbolizer defSym = new LineSymbolizer();
            defSym.ScaleMode = ScaleMode.Geographic;
            defSym.SetWidth(adjWidth);
            defSym.SetOutline(Color.Black, adjOutlineWidth);
            defSym.SetFillColor(Color.Gray);

            defCat.Symbolizer = defSym;
            defCat.SelectionSymbolizer = catSelSym;

            defCat.LegendText = "Not Surveyed";

            rdScheme.AddCategory(defCat);

            FeatureLayer roadFeatures = Layer as FeatureLayer;
            //roadFeatures
            if (    !string.IsNullOrEmpty(Project.settings.GetValue("road_labels")) &&
                    !string.IsNullOrEmpty(Project.settings.GetValue("road_f_streetname"))
                )
            {
                roadFeatures.AddLabels("[" + Project.settings.GetValue(ModuleName + "_f_streetname") + "]",
                        new Font("Tahoma", (float)8.0), Color.Black);
                roadFeatures.ShowLabels = true;
            }
            /*
            ((DotSpatial.Controls.MapLineLayer)Layer).ShowLabels = true;
            */
            ((MapLineLayer)Layer).Symbology = rdScheme;
            ((MapLineLayer)Layer).ApplyScheme(rdScheme);
        }

        private void clickPhotoBox(object sender, EventArgs e)
        {
            Panel_Road roadControls = getRoadControls();
            FormPicture largePic = new FormPicture();
            if (!string.IsNullOrWhiteSpace(roadControls.textBoxPhotoFile.Text))
            {
                try
                {
                    string imageLocation = Project.projectFolderPath + @"\Photos\" + roadControls.textBoxPhotoFile.Text;
                    
                    if (File.Exists(imageLocation))
                    {
                        largePic.pictureRoad.ImageLocation = imageLocation;
                    }
                    else
                    {
                        Log.Warning("Missing image file: " + imageLocation);
                        roadControls.toolTip.SetToolTip(roadControls.pictureBoxPhoto, "Missing: " + imageLocation);
                        throw new Exception("Missing image file");
                    }
                }
                catch
                {
                    largePic.pictureRoad.Image = Properties.Resources.error;
                }
            }
            else
            {
                largePic.pictureRoad.Image = Properties.Resources.nophoto;
            }
            largePic.Show();
        }

        public void showHistory(object sender, EventArgs e)
        {
            FeatureLayer selectionLayer = (FeatureLayer)Layer;
            ISelection shpSelection = selectionLayer.Selection;
            DataTable selectionTable = shpSelection.ToFeatureSet().DataTable;
            string histring = @"SELECT * FROM road WHERE TAMSID IN (" + extractTAMSIDs(selectionTable) + ") ORDER BY TAMSID ASC, survey_date DESC;";
            try
            {
                DataTable history = Database.GetDataByQuery(Project.conn, histring);
                FormHistory histForm = new FormHistory();
                histForm.dataGridViewHistory.DataSource = history;
                histForm.Show();
            }
            catch (Exception err)
            {
                Log.Error("Malformed request " + err.ToString());
                MessageBox.Show("An error occured when attempting to show history. Roads Database may have be corrupted.");
            }

        }

        public void generalReport(object sender, EventArgs e)
        {
            string[] da = { "Fatigue", "Edge Cracks", "Longitudinal", "Patches", "Potholes", "Drainage", "Transverse", "Blocking", "Rutting" };
            string[] dg = { "Potholes", "Rutting", "X-section", "Drainage", "Dust", "Aggregate", "Corrugation" };
            string[] dc = { "Spalling", "Joint Seals", "Corners", "Breaks", "Faulting", "Longitudinal", "Transverse", "Map Cracks", "Patches" };
            DataTable general = new DataTable();
            general.Columns.Add("ID");
            general.Columns.Add("Name");
            general.Columns.Add("From Address");
            general.Columns.Add("To Address");
            general.Columns.Add("Surface");
            general.Columns.Add("Governing Distress");
            general.Columns.Add("Treatment");
            general.Columns.Add("Cost");
            general.Columns.Add("Area");
            FeatureLayer selectionLayer = (FeatureLayer)Layer;
            selectionLayer.SelectAll();
            ISelection shpSelection = selectionLayer.Selection;
            DataTable selectionTable = shpSelection.ToFeatureSet().DataTable;
            string thisSql = SelectionSql.Replace("[[IDLIST]]", extractTAMSIDs(selectionTable));
            try
            {
                DataTable resultsTable = Database.GetDataByQuery(Project.conn, thisSql);
                double totalCost = 0;
                foreach (DataRow row in resultsTable.Rows)
                {
                    DataRow nr = general.NewRow();
                    string[] seld = da;
                    nr["ID"] = row["TAMSID"];
                    nr["Name"] = row["name"];
                    nr["From Address"] = row["from_address"];
                    nr["To Address"] = row["to_address"];
                    nr["Surface"] = row["surface"];
                    int distID = 0;
                    int maxRSL = 20;
                    if (row["surface"].ToString().Contains("asphalt"))
                    {
                        distID = 1;
                        maxRSL = 20;
                        seld = da;
                    }
                    else if (row["surface"].ToString().Contains("gravel"))
                    {
                        distID = 2;
                        maxRSL = 10;
                        seld = dg;
                    }
                    else if (row["surface"].ToString().Contains("concrete"))
                    {
                        distID = 3;
                        maxRSL = 20;
                        seld = dc;
                    }
                    nr["Governing Distress"] = "none";
                    nr["Cost"] = 0;
                    if (!row["suggested_treatment"].ToString().Contains("null") && !string.IsNullOrWhiteSpace(row["suggested_treatment"].ToString()))
                    {
                        nr["Treatment"] = row["suggested_treatment"];
                        string treatmentCost = Database.GetDataByQuery(Project.conn, "SELECT cost FROM treatments WHERE name = '" + row["suggested_treatment"].ToString() + "';").Rows[0]["cost"].ToString();
                        double estCost = Util.ToDouble(row["width"].ToString()) * Util.ToDouble(row["length"].ToString()) * Util.ToDouble(treatmentCost) /9;//Note: Treatment cost is per square yard. Road dimensions are in ft.
                        if (estCost > 1000000)
                        {
                            nr["Cost"] = Math.Round(estCost/1000000, 2).ToString() + "M";
                        }
                        else if (estCost > 1000)
                        {
                            nr["Cost"] = Math.Round(estCost / 1000).ToString() + "k";
                        }
                        else
                        {
                            nr["Cost"] = Math.Round(estCost).ToString();
                        }
                        totalCost += (int)estCost;
                    }
                    DataTable distresses = Database.GetDataByQuery(Project.conn, "SELECT * FROM road_distresses WHERE surface_id = " + distID.ToString());
                    for (int i = 1; i <= distresses.Rows.Count; i++)
                    {
                        int distValue = Util.ToInt(row["distress" + i.ToString()].ToString());
                        if (distValue <= 0)
                        {
                            continue;
                        }
                        int rsl = Util.ToInt(distresses.Rows[i-1]["rsl" + distValue.ToString()].ToString());
                        if (rsl < maxRSL)
                        {
                            maxRSL = rsl;
                            nr["Governing Distress"] = seld[i-1];
                        }
                    }
                    nr["Area"] = Util.ToDouble(row["width"].ToString()) * Util.ToDouble(row["length"].ToString());
                    general.Rows.Add(nr);
                }
                general.DefaultView.Sort = "Name asc, Treatment asc, From Address asc";
                general = general.DefaultView.ToTable();
                DataRow totals = general.NewRow();
                totals["Name"] = "Total";
                totals["From Address"] = "Estimated";
                totals["To Address"] = "Cost";
                if (totalCost > 1000000)
                {
                    totals["Cost"] = Math.Round(totalCost / 1000000, 2).ToString() + "M";
                }
                else if (totalCost > 1000)
                {
                    totals["Cost"] = Math.Round(totalCost / 1000).ToString() + "k";
                }
                else
                {
                    totals["Cost"] = Math.Round(totalCost).ToString();
                }
                general.Rows.Add(totals);
                FormOutput report = new FormOutput();
                report.dataGridViewReport.DataSource = general.DefaultView.ToTable();
                report.Text = "Treatment Report";
                report.Show();
            }
            catch (Exception err)
            {
                Log.Error("Could not get database values for " + ModuleName + " module.\n" + err.ToString());
                MessageBox.Show("An error has occured while trying to consolidate data.");
            }
            selectionLayer.ClearSelection();
        }

        public void potholeReport(object sender, EventArgs e)
        {
            string[] pd = {"less than 1\"", "less than 2\"", "more than 2\""};
            string[] pq = { "less than 2", "less than 5", "more than 5" };
            DataTable potholes = new DataTable("Potholes");
            potholes.Columns.Add("ID");
            potholes.Columns.Add("Name");
            potholes.Columns.Add("From Address");
            potholes.Columns.Add("To Address");
            potholes.Columns.Add("Depth");
            potholes.Columns.Add("Quantity");
            potholes.Columns.Add("Suggested Treatment");
            FeatureLayer selectionLayer = (FeatureLayer)Layer;
            selectionLayer.SelectAll();
            ISelection shpSelection = selectionLayer.Selection;
            DataTable selectionTable = shpSelection.ToFeatureSet().DataTable;
            string thisSql = SelectionSql.Replace("[[IDLIST]]", extractTAMSIDs(selectionTable));
            try
            {
                DataTable resultsTable = Database.GetDataByQuery(Project.conn, thisSql);
                foreach (DataRow row in resultsTable.Rows)
                {
                    if (Util.ToInt(row["distress5"].ToString()) <= 0)
                    {
                        continue;
                    }
                    DataRow nr = potholes.NewRow();
                    nr["ID"] = row["TAMSID"];
                    nr["Name"] = row["name"];
                    nr["From Address"] = row["from_address"];
                    nr["To Address"] = row["to_address"];
                    nr["Depth"] = (Util.ToInt(row["distress5"].ToString()) > 0 ? pd[(Util.ToInt(row["distress5"].ToString()) - 1)/3]: "None");
                    nr["Quantity"] = (Util.ToInt(row["distress5"].ToString()) > 0 ? pq[(Util.ToInt(row["distress5"].ToString()) - 1) % 3]: "None");
                    nr["Suggested Treatment"] = row["suggested_treatment"].ToString();
                    potholes.Rows.InsertAt(nr, potholes.Rows.Count);
                }
                potholes.DefaultView.Sort = "Name asc, From Address asc";
                FormOutput report = new FormOutput();
                report.dataGridViewReport.DataSource = potholes.DefaultView.ToTable();
                report.Text = "Potholes Report";
                report.Show();
            }
            catch (Exception err)
            {
                Log.Error("Could not get database values for " + ModuleName + " module.\n" + err.ToString());
                MessageBox.Show("An error has occured while trying to consolidate data.");
            }
            selectionLayer.ClearSelection();
        }

        public void reportSelected(object sender, EventArgs e)
        {
            string[] da = { "Fatigue", "Edge Cracks", "Longitudinal", "Patches", "Potholes", "Drainage", "Transverse", "Blocking", "Rutting" };
            string[] dg = { "Potholes", "Rutting", "X-section", "Drainage", "Dust", "Aggregate", "Corrugation" };
            string[] dc = { "Spalling", "Joint Seals", "Corners", "Breaks", "Faulting", "Longitudinal", "Transverse", "Map Cracks", "Patches" };
            DataTable general = new DataTable();
            general.Columns.Add("ID");
            general.Columns.Add("Name");
            general.Columns.Add("From Address");
            general.Columns.Add("To Address");
            general.Columns.Add("Surface");
            general.Columns.Add("Governing Distress");
            general.Columns.Add("Treatment");
            general.Columns.Add("Cost");
            general.Columns.Add("Area");
            FeatureLayer selectionLayer = (FeatureLayer)Layer;
            ISelection shpSelection = selectionLayer.Selection;
            DataTable selectionTable = shpSelection.ToFeatureSet().DataTable;
            string thisSql = SelectionSql.Replace("[[IDLIST]]", extractTAMSIDs(selectionTable));
            try
            {
                DataTable resultsTable = Database.GetDataByQuery(Project.conn, thisSql);
                double totalCost = 0;
                foreach (DataRow row in resultsTable.Rows)
                {
                    DataRow nr = general.NewRow();
                    string[] seld = da;
                    nr["ID"] = row["TAMSID"];
                    nr["Name"] = row["name"];
                    nr["From Address"] = row["from_address"];
                    nr["To Address"] = row["to_address"];
                    nr["Surface"] = row["surface"];
                    int distID = 0;
                    int maxRSL = 20;
                    if (row["surface"].ToString().Contains("asphalt"))
                    {
                        distID = 1;
                        maxRSL = 20;
                        seld = da;
                    }
                    else if (row["surface"].ToString().Contains("gravel"))
                    {
                        distID = 2;
                        maxRSL = 10;
                        seld = dg;
                    }
                    else if (row["surface"].ToString().Contains("concrete"))
                    {
                        distID = 3;
                        maxRSL = 20;
                        seld = dc;
                    }
                    nr["Governing Distress"] = "none";
                    nr["Cost"] = 0;
                    if (!row["suggested_treatment"].ToString().Contains("null") && !string.IsNullOrWhiteSpace(row["suggested_treatment"].ToString()))
                    {
                        nr["Treatment"] = row["suggested_treatment"];
                        string treatmentCost = Database.GetDataByQuery(Project.conn, "SELECT cost FROM treatments WHERE name = '" + row["suggested_treatment"].ToString() + "';").Rows[0]["cost"].ToString();
                        double estCost = Util.ToDouble(row["width"].ToString()) * Util.ToDouble(row["length"].ToString()) * Util.ToDouble(treatmentCost) / 9;//Note: Treatment cost is per square yard. Road dimensions are in ft.
                        if (estCost > 1000000)
                        {
                            nr["Cost"] = Math.Round(estCost / 1000000, 2).ToString() + "M";
                        }
                        else if (estCost > 1000)
                        {
                            nr["Cost"] = Math.Round(estCost / 1000).ToString() + "k";
                        }
                        else
                        {
                            nr["Cost"] = Math.Round(estCost).ToString();
                        }
                        totalCost += (int)estCost;
                    }
                    DataTable distresses = Database.GetDataByQuery(Project.conn, "SELECT * FROM road_distresses WHERE surface_id = " + distID.ToString());
                    for (int i = 1; i <= distresses.Rows.Count; i++)
                    {
                        int distValue = Util.ToInt(row["distress" + i.ToString()].ToString());
                        if (distValue <= 0)
                        {
                            continue;
                        }
                        int rsl = Util.ToInt(distresses.Rows[i - 1]["rsl" + distValue.ToString()].ToString());
                        if (rsl < maxRSL)
                        {
                            maxRSL = rsl;
                            nr["Governing Distress"] = seld[i - 1];
                        }
                    }
                    nr["Area"] = Util.ToDouble(row["width"].ToString()) * Util.ToDouble(row["length"].ToString());
                    general.Rows.Add(nr);
                }
                general.DefaultView.Sort = "Name asc, Treatment asc, From Address asc";
                general = general.DefaultView.ToTable();
                DataRow totals = general.NewRow();
                totals["Name"] = "Total";
                totals["From Address"] = "Estimated";
                totals["To Address"] = "Cost";
                if (totalCost > 1000000)
                {
                    totals["Cost"] = Math.Round(totalCost / 1000000, 2).ToString() + "M";
                }
                else if (totalCost > 1000)
                {
                    totals["Cost"] = Math.Round(totalCost / 1000).ToString() + "k";
                }
                else
                {
                    totals["Cost"] = Math.Round(totalCost).ToString();
                }
                general.Rows.Add(totals);
                FormOutput report = new FormOutput();
                report.dataGridViewReport.DataSource = general.DefaultView.ToTable();
                report.Text = "Treatment Report";
                report.Show();
            }
            catch (Exception err)
            {
                Log.Error("Could not get database values for " + ModuleName + " module.\n" + err.ToString());
                MessageBox.Show("An error has occured while trying to consolidate data.");
            }
        }

        protected void openBudgetTool(object sender, EventArgs e)
        {
            FeatureLayer selectionLayer = (FeatureLayer)Layer;
            selectionLayer.SelectAll();
            ISelection shpSelection = selectionLayer.Selection;
            DataTable selectionTable = shpSelection.ToFeatureSet().DataTable;
            string roadSQL = SelectionSql.Replace("[[IDLIST]]", extractTAMSIDs(selectionTable));
            string treatmentSQL = "SELECT * FROM treatments";
            DataTable roads = Database.GetDataByQuery(Project.conn, roadSQL);
            DataTable treatments = Database.GetDataByQuery(Project.conn, treatmentSQL);
            roads.DefaultView.Sort = "rsl asc";
            treatments.DefaultView.Sort = "id asc";
            FormBudgetEstimator budget = new FormBudgetEstimator();
            selectionLayer.ClearSelection();
            if (budget.setData(roads.DefaultView.ToTable(), treatments.DefaultView.ToTable()))
            {
                budget.Show();
            }
            else
            {
                budget.Close();
            }
            
        }
        
        protected void toggleColors(object sender, EventArgs e)
        {
            FeatureLayer selectionLayer = (FeatureLayer)Layer;
            bool reselect = false;
            string id = "1";
            string tamsidcolumn = Project.settings.GetValue(ModuleName + "_f_TAMSID");
            if (selectionLayer.Selection.Count > 0)
            {
                reselect = true;
                id = selectionLayer.Selection.ToFeatureSet().DataTable.Rows[0][tamsidcolumn].ToString();
            }
            colorsOn = colorsOn ? false : true;
            setSymbolizer();
            if (reselect)
            {
                selectionLayer.SelectByAttribute(tamsidcolumn + " = " + id);
            }
        }

    }
}