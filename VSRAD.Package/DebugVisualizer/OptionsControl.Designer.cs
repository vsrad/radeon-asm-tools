namespace VSGCN.Package.DebugVisualizer
{
    partial class OptionsControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.layout = new System.Windows.Forms.TableLayoutPanel();
            this.groupConnection = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.flowLayoutConnection = new System.Windows.Forms.FlowLayoutPanel();
            this.labelRemoteMachine = new System.Windows.Forms.Label();
            this.textRemoteMachine = new System.Windows.Forms.TextBox();
            this.labelRemotePort = new System.Windows.Forms.Label();
            this.groupDebugger = new System.Windows.Forms.GroupBox();
            this.layoutDebugger = new System.Windows.Forms.TableLayoutPanel();
            this.textBreakpointScriptArgs = new System.Windows.Forms.TextBox();
            this.labelBreakpointScriptArgs = new System.Windows.Forms.Label();
            this.textArgs = new System.Windows.Forms.TextBox();
            this.labelArgs = new System.Windows.Forms.Label();
            this.groupWatches = new System.Windows.Forms.GroupBox();
            this.layoutWatches = new System.Windows.Forms.TableLayoutPanel();
            this.checkMaskLanes = new System.Windows.Forms.CheckBox();
            this.checkShowSystem = new System.Windows.Forms.CheckBox();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.labelLaneGrouping = new System.Windows.Forms.Label();
            this.NDRange3D = new System.Windows.Forms.CheckBox();
            this.groupRegions = new System.Windows.Forms.GroupBox();
            this.tableRegions = new System.Windows.Forms.DataGridView();
            this.buttonSave = new System.Windows.Forms.Button();
            this.optionRemotePort = new VSGCN.Package.DebugVisualizer.NumberControl();
            this.optionLaneGrouping = new VSGCN.Package.DebugVisualizer.NumberControl();
            this.layout.SuspendLayout();
            this.groupConnection.SuspendLayout();
            this.tableLayoutPanel1.SuspendLayout();
            this.flowLayoutConnection.SuspendLayout();
            this.groupDebugger.SuspendLayout();
            this.layoutDebugger.SuspendLayout();
            this.groupWatches.SuspendLayout();
            this.layoutWatches.SuspendLayout();
            this.flowLayoutPanel1.SuspendLayout();
            this.groupRegions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tableRegions)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.optionRemotePort)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.optionLaneGrouping)).BeginInit();
            this.SuspendLayout();
            // 
            // layout
            // 
            this.layout.BackColor = System.Drawing.SystemColors.Window;
            this.layout.ColumnCount = 1;
            this.layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.layout.Controls.Add(this.groupConnection, 0, 3);
            this.layout.Controls.Add(this.groupDebugger, 0, 2);
            this.layout.Controls.Add(this.groupWatches, 0, 1);
            this.layout.Controls.Add(this.groupRegions, 0, 0);
            this.layout.Controls.Add(this.buttonSave, 0, 4);
            this.layout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.layout.Location = new System.Drawing.Point(0, 0);
            this.layout.Margin = new System.Windows.Forms.Padding(2);
            this.layout.Name = "layout";
            this.layout.RowCount = 5;
            this.layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 23.81466F));
            this.layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 29.93984F));
            this.layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 31.16545F));
            this.layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 15.08006F));
            this.layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 23F));
            this.layout.Size = new System.Drawing.Size(450, 471);
            this.layout.TabIndex = 0;
            // 
            // groupConnection
            // 
            this.groupConnection.Controls.Add(this.tableLayoutPanel1);
            this.groupConnection.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupConnection.Location = new System.Drawing.Point(6, 382);
            this.groupConnection.Margin = new System.Windows.Forms.Padding(6, 3, 6, 6);
            this.groupConnection.Name = "groupConnection";
            this.groupConnection.Padding = new System.Windows.Forms.Padding(2);
            this.groupConnection.Size = new System.Drawing.Size(438, 58);
            this.groupConnection.TabIndex = 2;
            this.groupConnection.TabStop = false;
            this.groupConnection.Text = "Server connection";
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.flowLayoutConnection, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(2, 15);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.Padding = new System.Windows.Forms.Padding(6, 3, 6, 6);
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(434, 41);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // flowLayoutConnection
            // 
            this.flowLayoutConnection.Controls.Add(this.labelRemoteMachine);
            this.flowLayoutConnection.Controls.Add(this.textRemoteMachine);
            this.flowLayoutConnection.Controls.Add(this.labelRemotePort);
            this.flowLayoutConnection.Controls.Add(this.optionRemotePort);
            this.flowLayoutConnection.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutConnection.Location = new System.Drawing.Point(8, 5);
            this.flowLayoutConnection.Margin = new System.Windows.Forms.Padding(2);
            this.flowLayoutConnection.Name = "flowLayoutConnection";
            this.flowLayoutConnection.Size = new System.Drawing.Size(418, 96);
            this.flowLayoutConnection.TabIndex = 0;
            // 
            // labelRemoteMachine
            // 
            this.labelRemoteMachine.Location = new System.Drawing.Point(2, 0);
            this.labelRemoteMachine.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelRemoteMachine.Name = "labelRemoteMachine";
            this.labelRemoteMachine.Size = new System.Drawing.Size(99, 20);
            this.labelRemoteMachine.TabIndex = 7;
            this.labelRemoteMachine.Text = "Remote machine:";
            this.labelRemoteMachine.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // textRemoteMachine
            // 
            this.textRemoteMachine.Location = new System.Drawing.Point(105, 2);
            this.textRemoteMachine.Margin = new System.Windows.Forms.Padding(2);
            this.textRemoteMachine.Name = "textRemoteMachine";
            this.textRemoteMachine.Size = new System.Drawing.Size(160, 20);
            this.textRemoteMachine.TabIndex = 8;
            // 
            // labelRemotePort
            // 
            this.labelRemotePort.Location = new System.Drawing.Point(269, 0);
            this.labelRemotePort.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelRemotePort.Name = "labelRemotePort";
            this.labelRemotePort.Size = new System.Drawing.Size(38, 20);
            this.labelRemotePort.TabIndex = 9;
            this.labelRemotePort.Text = "Port:";
            this.labelRemotePort.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // groupDebugger
            // 
            this.groupDebugger.Controls.Add(this.layoutDebugger);
            this.groupDebugger.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupDebugger.Location = new System.Drawing.Point(6, 243);
            this.groupDebugger.Margin = new System.Windows.Forms.Padding(6, 3, 6, 6);
            this.groupDebugger.Name = "groupDebugger";
            this.groupDebugger.Padding = new System.Windows.Forms.Padding(2);
            this.groupDebugger.Size = new System.Drawing.Size(438, 130);
            this.groupDebugger.TabIndex = 1;
            this.groupDebugger.TabStop = false;
            this.groupDebugger.Text = "Debugger";
            // 
            // layoutDebugger
            // 
            this.layoutDebugger.ColumnCount = 1;
            this.layoutDebugger.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.layoutDebugger.Controls.Add(this.textBreakpointScriptArgs, 0, 3);
            this.layoutDebugger.Controls.Add(this.labelBreakpointScriptArgs, 0, 2);
            this.layoutDebugger.Controls.Add(this.textArgs, 0, 1);
            this.layoutDebugger.Controls.Add(this.labelArgs, 0, 0);
            this.layoutDebugger.Dock = System.Windows.Forms.DockStyle.Fill;
            this.layoutDebugger.Location = new System.Drawing.Point(2, 15);
            this.layoutDebugger.Margin = new System.Windows.Forms.Padding(0);
            this.layoutDebugger.Name = "layoutDebugger";
            this.layoutDebugger.Padding = new System.Windows.Forms.Padding(6, 3, 6, 6);
            this.layoutDebugger.RowCount = 5;
            this.layoutDebugger.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 16F));
            this.layoutDebugger.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.layoutDebugger.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.layoutDebugger.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.layoutDebugger.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 16F));
            this.layoutDebugger.Size = new System.Drawing.Size(434, 113);
            this.layoutDebugger.TabIndex = 0;
            // 
            // textBreakpointScriptArgs
            // 
            this.textBreakpointScriptArgs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBreakpointScriptArgs.Location = new System.Drawing.Point(8, 71);
            this.textBreakpointScriptArgs.Margin = new System.Windows.Forms.Padding(2);
            this.textBreakpointScriptArgs.Name = "textBreakpointScriptArgs";
            this.textBreakpointScriptArgs.Size = new System.Drawing.Size(418, 20);
            this.textBreakpointScriptArgs.TabIndex = 7;
            // 
            // labelBreakpointScriptArgs
            // 
            this.labelBreakpointScriptArgs.Location = new System.Drawing.Point(8, 49);
            this.labelBreakpointScriptArgs.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelBreakpointScriptArgs.Name = "labelBreakpointScriptArgs";
            this.labelBreakpointScriptArgs.Size = new System.Drawing.Size(150, 18);
            this.labelBreakpointScriptArgs.TabIndex = 6;
            this.labelBreakpointScriptArgs.Text = "Breakpoint Script Args (-p):";
            // 
            // textArgs
            // 
            this.textArgs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textArgs.Location = new System.Drawing.Point(8, 21);
            this.textArgs.Margin = new System.Windows.Forms.Padding(2);
            this.textArgs.Name = "textArgs";
            this.textArgs.Size = new System.Drawing.Size(418, 20);
            this.textArgs.TabIndex = 1;
            // 
            // labelArgs
            // 
            this.labelArgs.Location = new System.Drawing.Point(8, 3);
            this.labelArgs.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelArgs.Name = "labelArgs";
            this.labelArgs.Size = new System.Drawing.Size(150, 16);
            this.labelArgs.TabIndex = 5;
            this.labelArgs.Text = "Application Arguments (-v):";
            // 
            // groupWatches
            // 
            this.groupWatches.Controls.Add(this.layoutWatches);
            this.groupWatches.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupWatches.Location = new System.Drawing.Point(6, 112);
            this.groupWatches.Margin = new System.Windows.Forms.Padding(6, 6, 6, 3);
            this.groupWatches.Name = "groupWatches";
            this.groupWatches.Padding = new System.Windows.Forms.Padding(2);
            this.groupWatches.Size = new System.Drawing.Size(438, 125);
            this.groupWatches.TabIndex = 0;
            this.groupWatches.TabStop = false;
            this.groupWatches.Text = "Watches";
            // 
            // layoutWatches
            // 
            this.layoutWatches.ColumnCount = 1;
            this.layoutWatches.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.layoutWatches.Controls.Add(this.checkMaskLanes, 0, 1);
            this.layoutWatches.Controls.Add(this.checkShowSystem, 0, 0);
            this.layoutWatches.Controls.Add(this.flowLayoutPanel1, 0, 2);
            this.layoutWatches.Controls.Add(this.NDRange3D, 0, 3);
            this.layoutWatches.Dock = System.Windows.Forms.DockStyle.Fill;
            this.layoutWatches.Location = new System.Drawing.Point(2, 15);
            this.layoutWatches.Margin = new System.Windows.Forms.Padding(0);
            this.layoutWatches.Name = "layoutWatches";
            this.layoutWatches.Padding = new System.Windows.Forms.Padding(6, 3, 6, 6);
            this.layoutWatches.RowCount = 4;
            this.layoutWatches.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 23F));
            this.layoutWatches.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 23F));
            this.layoutWatches.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 23F));
            this.layoutWatches.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.layoutWatches.Size = new System.Drawing.Size(434, 108);
            this.layoutWatches.TabIndex = 0;
            // 
            // checkMaskLanes
            // 
            this.checkMaskLanes.AutoSize = true;
            this.checkMaskLanes.Location = new System.Drawing.Point(9, 29);
            this.checkMaskLanes.Name = "checkMaskLanes";
            this.checkMaskLanes.Size = new System.Drawing.Size(293, 17);
            this.checkMaskLanes.TabIndex = 1;
            this.checkMaskLanes.Text = "Use data from lanes 8:9 (exec mask) to gray out columns\r\n";
            this.checkMaskLanes.UseVisualStyleBackColor = true;
            // 
            // checkShowSystem
            // 
            this.checkShowSystem.AutoSize = true;
            this.checkShowSystem.Location = new System.Drawing.Point(8, 5);
            this.checkShowSystem.Margin = new System.Windows.Forms.Padding(2);
            this.checkShowSystem.Name = "checkShowSystem";
            this.checkShowSystem.Size = new System.Drawing.Size(165, 17);
            this.checkShowSystem.TabIndex = 0;
            this.checkShowSystem.Text = "Show hidden System variable";
            this.checkShowSystem.UseVisualStyleBackColor = true;
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.labelLaneGrouping);
            this.flowLayoutPanel1.Controls.Add(this.optionLaneGrouping);
            this.flowLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanel1.Location = new System.Drawing.Point(8, 51);
            this.flowLayoutPanel1.Margin = new System.Windows.Forms.Padding(2);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(418, 19);
            this.flowLayoutPanel1.TabIndex = 2;
            // 
            // labelLaneGrouping
            // 
            this.labelLaneGrouping.Location = new System.Drawing.Point(2, 0);
            this.labelLaneGrouping.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelLaneGrouping.Name = "labelLaneGrouping";
            this.labelLaneGrouping.Size = new System.Drawing.Size(84, 20);
            this.labelLaneGrouping.TabIndex = 6;
            this.labelLaneGrouping.Text = "Group lanes by:";
            this.labelLaneGrouping.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // NDRange3D
            // 
            this.NDRange3D.AutoSize = true;
            this.NDRange3D.Location = new System.Drawing.Point(9, 75);
            this.NDRange3D.Name = "NDRange3D";
            this.NDRange3D.Size = new System.Drawing.Size(91, 17);
            this.NDRange3D.TabIndex = 3;
            this.NDRange3D.Text = "3D NDRange";
            this.NDRange3D.UseVisualStyleBackColor = true;
            // 
            // groupRegions
            // 
            this.groupRegions.Controls.Add(this.tableRegions);
            this.groupRegions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupRegions.Location = new System.Drawing.Point(2, 2);
            this.groupRegions.Margin = new System.Windows.Forms.Padding(2);
            this.groupRegions.Name = "groupRegions";
            this.groupRegions.Padding = new System.Windows.Forms.Padding(6, 3, 6, 6);
            this.groupRegions.Size = new System.Drawing.Size(446, 102);
            this.groupRegions.TabIndex = 0;
            this.groupRegions.TabStop = false;
            this.groupRegions.Text = "Regions";
            // 
            // tableRegions
            // 
            this.tableRegions.BackgroundColor = System.Drawing.SystemColors.Window;
            this.tableRegions.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.tableRegions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableRegions.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.tableRegions.Location = new System.Drawing.Point(6, 16);
            this.tableRegions.Margin = new System.Windows.Forms.Padding(2);
            this.tableRegions.Name = "tableRegions";
            this.tableRegions.RowHeadersVisible = false;
            this.tableRegions.RowHeadersWidth = 51;
            this.tableRegions.RowTemplate.Height = 24;
            this.tableRegions.Size = new System.Drawing.Size(434, 80);
            this.tableRegions.TabIndex = 2;
            // 
            // buttonSave
            // 
            this.buttonSave.Dock = System.Windows.Forms.DockStyle.Right;
            this.buttonSave.Location = new System.Drawing.Point(392, 448);
            this.buttonSave.Margin = new System.Windows.Forms.Padding(2);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(56, 21);
            this.buttonSave.TabIndex = 3;
            this.buttonSave.Text = "Save";
            this.buttonSave.UseVisualStyleBackColor = true;
            // 
            // optionRemotePort
            // 
            this.optionRemotePort.BackColor = System.Drawing.SystemColors.Window;
            this.optionRemotePort.Invalid = false;
            this.optionRemotePort.Location = new System.Drawing.Point(311, 2);
            this.optionRemotePort.Margin = new System.Windows.Forms.Padding(2);
            this.optionRemotePort.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this.optionRemotePort.Name = "optionRemotePort";
            this.optionRemotePort.Size = new System.Drawing.Size(70, 20);
            this.optionRemotePort.TabIndex = 10;
            this.optionRemotePort.Value = ((uint)(0u));
            // 
            // optionLaneGrouping
            // 
            this.optionLaneGrouping.BackColor = System.Drawing.SystemColors.Window;
            this.optionLaneGrouping.Invalid = false;
            this.optionLaneGrouping.Location = new System.Drawing.Point(90, 2);
            this.optionLaneGrouping.Margin = new System.Windows.Forms.Padding(2);
            this.optionLaneGrouping.Maximum = new decimal(new int[] {
            512,
            0,
            0,
            0});
            this.optionLaneGrouping.Name = "optionLaneGrouping";
            this.optionLaneGrouping.Size = new System.Drawing.Size(70, 20);
            this.optionLaneGrouping.TabIndex = 7;
            this.optionLaneGrouping.Value = ((uint)(0u));
            // 
            // OptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.layout);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "OptionsControl";
            this.Size = new System.Drawing.Size(450, 471);
            this.layout.ResumeLayout(false);
            this.groupConnection.ResumeLayout(false);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.flowLayoutConnection.ResumeLayout(false);
            this.flowLayoutConnection.PerformLayout();
            this.groupDebugger.ResumeLayout(false);
            this.layoutDebugger.ResumeLayout(false);
            this.layoutDebugger.PerformLayout();
            this.groupWatches.ResumeLayout(false);
            this.layoutWatches.ResumeLayout(false);
            this.layoutWatches.PerformLayout();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.groupRegions.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.tableRegions)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.optionRemotePort)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.optionLaneGrouping)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel layout;
        private System.Windows.Forms.Label labelArgs;
        private System.Windows.Forms.TextBox textArgs;
        private System.Windows.Forms.GroupBox groupDebugger;
        private System.Windows.Forms.TableLayoutPanel layoutDebugger;
        private System.Windows.Forms.GroupBox groupWatches;
        private System.Windows.Forms.TableLayoutPanel layoutWatches;
        private System.Windows.Forms.CheckBox checkShowSystem;
        private System.Windows.Forms.GroupBox groupRegions;
        private System.Windows.Forms.DataGridView tableRegions;
        private System.Windows.Forms.CheckBox checkMaskLanes;
        private System.Windows.Forms.TextBox textBreakpointScriptArgs;
        private System.Windows.Forms.Label labelBreakpointScriptArgs;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Label labelLaneGrouping;
        private NumberControl optionLaneGrouping;
        private System.Windows.Forms.GroupBox groupConnection;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutConnection;
        private System.Windows.Forms.Label labelRemoteMachine;
        private System.Windows.Forms.TextBox textRemoteMachine;
        private System.Windows.Forms.Label labelRemotePort;
        private NumberControl optionRemotePort;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.CheckBox NDRange3D;
    }
}
