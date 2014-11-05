//
// DO NOT REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
//
// @Authors:
//       timop
//       Tim Ingham, alanP
//
// Copyright 2004-2014 by OM International
//
// This file is part of OpenPetra.org.
//
// OpenPetra.org is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// OpenPetra.org is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with OpenPetra.org.  If not, see <http://www.gnu.org/licenses/>.
//
using System;
using System.Windows.Forms;
using System.Data;
using Ict.Common;
using Ict.Common.Controls;
using Ict.Petra.Shared;
using Ict.Petra.Shared.MPartner;
using Ict.Petra.Client.MPartner.Gui;
using Ict.Petra.Client.App.Core;
using Ict.Petra.Client.CommonForms;
using Ict.Petra.Client.MCommon;
using Ict.Petra.Shared.Interfaces.MFinance;
using System.Threading;

namespace Ict.Petra.Client.MFinance.Gui.AP
{
    public partial class TUC_Suppliers
    {
        private bool FKeepUpSearchFinishedCheck = false;

        /// <summary>DataTables that holds all Pages of data (also empty ones that are not retrieved yet!)</summary>
        private DataTable FSupplierTable = null;
        private TSgrdDataGridPaged grdDetails = null;
        private int FPrevRowChangedRow = -1;
        private DataRow FPreviouslySelectedDetailRow = null;

        private TFrmAPMain FMainForm = null;


        /// <summary>
        /// Sets the reference to the main form (the host of this user control)
        /// </summary>
        public TFrmAPMain MainForm
        {
            set
            {
                FMainForm = value;
            }
        }

        private void InitializeManualCode()
        {
            // The auto-generated code requires that the grid be named grdDetails (for filter/find), but that doesn't work for another part of the autogenerated code!
            // So we make grdDetails reference grdSuppliers here at initialization
            grdDetails = grdSuppliers;

            grdSuppliers.DataPageLoaded += new TDataPageLoadedEventHandler(grdSuppliers_DataPageLoaded);
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////
        // These methods are stubs that allow the auto-generated code to compile (we don't have a details panel)
        // Also, although the template implements Filter/Find, it doesn't implement IGridBase

        /// <summary>
        /// Method required by IGridBase.
        /// </summary>
        public void SelectRowInGrid(int ARowNumber)
        {
            if (ARowNumber >= grdSuppliers.Rows.Count)
            {
                ARowNumber = grdSuppliers.Rows.Count - 1;
            }

            if ((ARowNumber < 1) && (grdSuppliers.Rows.Count > 1))
            {
                ARowNumber = 1;
            }

            grdSuppliers.SelectRowInGrid(ARowNumber);
            FPrevRowChangedRow = ARowNumber;
        }

        /// <summary>
        /// Standard method
        /// </summary>
        public void GetDataFromControls()
        {
        }

        /// /////////////////////////////////////////////////////////////////////////////////////////////////////////


        public void InitializeGUI(TFrmAPMain AMainForm)
        {
            FMainForm = AMainForm;

            TFrmPetraUtils utils = FMainForm.GetPetraUtilsObject();
            utils.SetStatusBarText(grdSuppliers, Catalog.GetString("Double-click a supplier to see the transaction history"));
            utils.SetStatusBarText(btnCreateCreditNote, Catalog.GetString("Click to create a credit note for the selected supplier"));
            utils.SetStatusBarText(btnCreateInvoice, Catalog.GetString("Click to create an invoice for the selected supplier"));
            utils.SetStatusBarText(btnEditDetails, Catalog.GetString("Click to edit the details for the selected supplier"));
            utils.SetStatusBarText(btnNewSupplier, Catalog.GetString("Click to create a new supplier"));
            //utils.SetStatusBarText(btnSupplierTransactions, Catalog.GetString("Click to view the transaction history for the selected supplier"));
            utils.SetStatusBarText(chkToggleFilter, Catalog.GetString("Click to show/hide the Filter/Find panel"));
        }

        /// <summary>
        /// Called when the main screen is activated
        /// </summary>
        public void RunOnceOnParentActivationManual()
        {
            // We need to populate the Currency box on the filter panel since it has not been cloned from a details panel
            //  which is what 'normally' happens

            // Create a control to clone from
            Ict.Petra.Client.CommonControls.TCmbAutoPopulated cmbSource = new CommonControls.TCmbAutoPopulated();
            cmbSource.ListTable = CommonControls.TCmbAutoPopulated.TListTableEnum.CurrencyCodeList;
            cmbSource.InitialiseUserControl();

            // Populate our filter combo from this one
            TCmbAutoComplete cmbCurrency = (TCmbAutoComplete)FFilterAndFindObject.FilterPanelControls.FindControlByName("cmbCurrency");
            cmbCurrency.DisplayMember = cmbSource.cmbCombobox.DisplayMember;
            cmbCurrency.ValueMember = cmbSource.cmbCombobox.ValueMember;
            cmbCurrency.DataSource = ((DataView)cmbSource.cmbCombobox.DataSource).ToTable().DefaultView;
        }

        /// <summary>
        /// Call this method to load suppliers into the user control
        /// </summary>
        public void LoadSuppliers()
        {
            if (FKeepUpSearchFinishedCheck)
            {
                // don't run several searches at the same time
                return;
            }

            if (FSupplierTable != null)
            {
                // we have loaded the results already
                if (!FMainForm.IsSupplierDataChanged)
                {
                    grdSuppliers.Focus();
                    return;
                }
            }

            this.Cursor = Cursors.WaitCursor;

            DataTable CriteriaTable = new DataTable();
            CriteriaTable.Columns.Add("LedgerNumber", typeof(Int32));
            CriteriaTable.Columns.Add("SupplierId", typeof(string));
            CriteriaTable.Columns.Add("DaysPlus", typeof(decimal));

            // From 2014 we load all the data so the only criteria of interest is the ledger number
            DataRow row = CriteriaTable.NewRow();
            row["DaysPlus"] = -1;
            row["SupplierId"] = String.Empty;
            row["LedgerNumber"] = FMainForm.LedgerNumber;
            CriteriaTable.Rows.Add(row);

            // Start the asynchronous search operation on the PetraServer
            grdSuppliers.DataSource = null;
            FSupplierTable = null;
            FMainForm.SupplierFindObject.FindSupplier(CriteriaTable);

            // Start thread that checks for the end of the search operation on the PetraServer
            FKeepUpSearchFinishedCheck = true;
            Thread FinishedCheckThread = new Thread(new ThreadStart(SearchFinishedCheckThread));
            FinishedCheckThread.Start();
        }

        private void grdSuppliers_DataPageLoaded(object Sender, TDataPageLoadEventArgs e)
        {
            // This is where we end up after querying the database and loading the first data into the grid
            // We are back in our main thread here
            this.Cursor = Cursors.Default;

            if (e.DataPage == 0)
            {
                FMainForm.IsSupplierDataChanged = false;
            }
        }

        private delegate void SimpleDelegate();

        /// <summary>
        /// Thread for the search operation. Monitor's the Server System.Object's
        /// AsyncExecProgress.ProgressState and invokes UI updates from that.
        ///
        /// </summary>
        /// <returns>void</returns>
        private void SearchFinishedCheckThread()
        {
            TProgressState ThreadStatus;

            // Check whether this thread should still execute
            while (FKeepUpSearchFinishedCheck)
            {
                // Wait and see if anything has changed
                Thread.Sleep(200);

                try
                {
                    /* The next line of code calls a function on the PetraServer
                     * > causes a bit of data traffic everytime! */
                    ThreadStatus = FMainForm.SupplierFindObject.Progress;
                }
                catch (NullReferenceException)
                {
                    // The form is closing on the main thread ...
                    return;         // end this thread
                }
                catch (Exception)
                {
                    throw;
                }

                if (ThreadStatus.JobFinished)
                {
                    FKeepUpSearchFinishedCheck = false;

                    try
                    {
                        // see also http://stackoverflow.com/questions/6184/how-do-i-make-event-callbacks-into-my-win-forms-thread-safe
                        if (InvokeRequired)
                        {
                            Invoke(new SimpleDelegate(FinishThread));
                        }
                        else
                        {
                            FinishThread();
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Another exception that can be caused when the main screen is closed while running this thread
                        return;
                    }
                }
                else if (ThreadStatus.CancelJob)
                {
                    FKeepUpSearchFinishedCheck = false;
                    return;
                }

                // Loop again while FKeepUpSearchFinishedCheck is true ...
            }
        }

        private void FinishThread()
        {
            // Fetch the first page of data
            DataTable dataTable = null;

            try
            {
                grdSuppliers.MinimumPageSize = 200;
                dataTable = grdSuppliers.LoadFirstDataPage(@GetDataPagedResult);
            }
            catch (Exception E)
            {
                MessageBox.Show(E.ToString());
                return;
            }

            if (dataTable == null)
            {
                // we lost the supplierFind object - probably means the screen is closing down so quit now
                return;
            }

            InitialiseGrid();

            DataView myDataView = FSupplierTable.DefaultView;
            myDataView.AllowNew = false;
            grdSuppliers.DataSource = new DevAge.ComponentModel.BoundDataView(myDataView);

            SetSupplierFilters(null, null);
            string currentFilter = FFilterAndFindObject.CurrentActiveFilter;
            ApplyFilterManual(ref currentFilter);

            if (grdSuppliers.TotalPages > 0)
            {
                if (grdSuppliers.TotalPages > 1)
                {
                    // Now we can load the remaining pages ...
                    grdSuppliers.LoadAllDataPages();
                }

                // Highlight first Row
                SelectRowInGrid(1);
            }

            // Size it
            grdSuppliers.AutoResizeGrid();

            grdSuppliers.SetHeaderTooltip(0, Catalog.GetString("Supplier Key"));
            grdSuppliers.SetHeaderTooltip(1, Catalog.GetString("Supplier Name"));
            grdSuppliers.SetHeaderTooltip(2, Catalog.GetString("Currency"));
            grdSuppliers.SetHeaderTooltip(3, Catalog.GetString("Status"));

            UpdateRecordNumberDisplay();
            FMainForm.ActionEnabledEvent(null, new ActionEventArgs("cndSelectedSupplier", grdSuppliers.TotalPages > 0));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="ANeededPage"></param>
        /// <param name="APageSize"></param>
        /// <param name="ATotalRecords"></param>
        /// <param name="ATotalPages"></param>
        /// <returns></returns>
        private DataTable GetDataPagedResult(Int16 ANeededPage, Int16 APageSize, out Int32 ATotalRecords, out Int16 ATotalPages)
        {
            ATotalRecords = 0;
            ATotalPages = 0;

            IAPUIConnectorsFind findObject = FMainForm.SupplierFindObject;

            if (findObject != null)
            {
                DataTable NewPage = findObject.GetDataPagedResult(ANeededPage, APageSize, out ATotalRecords, out ATotalPages);

                if (FSupplierTable == null)
                {
                    FSupplierTable = NewPage;
                }

                return NewPage;
            }

            return null;
        }

        private void InitialiseGrid()
        {
            grdSuppliers.Columns.Clear();
            grdSuppliers.AddTextColumn(Catalog.GetString("Supplier Key"), FSupplierTable.Columns[0]);
            grdSuppliers.AddTextColumn(Catalog.GetString("Supplier Name"), FSupplierTable.Columns[1]);
            grdSuppliers.AddTextColumn(Catalog.GetString("Currency"), FSupplierTable.Columns[2]);
            grdSuppliers.AddTextColumn(Catalog.GetString("Status"), FSupplierTable.Columns[3]);
        }

        private void SetSupplierFilters(object sender, EventArgs e)
        {
            if (FSupplierTable != null)
            {
                string filter = String.Empty;
                string filterJoint = " AND ";

                TextBox txtSupplierName = (TextBox)FFilterAndFindObject.FilterPanelControls.FindControlByName("txtSupplierName");

                if (txtSupplierName.Text.Trim().Length > 0)
                {
                    filter += String.Format("(PartnerShortName LIKE '%{0}%')", txtSupplierName.Text.Trim());
                }

                TCmbAutoComplete cmbCurrency = (TCmbAutoComplete)FFilterAndFindObject.FilterPanelControls.FindControlByName("cmbCurrency");

                if (cmbCurrency.Text.Length > 0)
                {
                    if (filter.Length > 0)
                    {
                        filter += filterJoint;
                    }

                    filter += String.Format("(CurrencyCode='{0}')", cmbCurrency.Text);
                }

                RadioButton rbtActiveSuppliers = (RadioButton)FFilterAndFindObject.FilterPanelControls.FindControlByName("rbtActiveSuppliers");

                if (rbtActiveSuppliers.Checked)
                {
                    if (filter.Length > 0)
                    {
                        filter += filterJoint;
                    }

                    filter += "(StatusCode='ACTIVE')";
                }

                RadioButton rbtInactiveSuppliers = (RadioButton)FFilterAndFindObject.FilterPanelControls.FindControlByName("rbtInactiveSuppliers");

                if (rbtInactiveSuppliers.Checked)
                {
                    if (filter.Length > 0)
                    {
                        filter += filterJoint;
                    }

                    filter += "(StatusCode='INACTIVE')";
                }

                FFilterAndFindObject.FilterPanelControls.SetBaseFilter(filter, filter.Length == 0);
            }
        }

        /// <summary>
        /// get the partner key of the currently selected supplier in the grid
        /// </summary>
        /// <returns></returns>
        private Int64 GetCurrentlySelectedSupplier()
        {
            DataRowView[] SelectedGridRow = grdSuppliers.SelectedDataRowsAsDataRowView;
            Int64 SupplierKey = -1;

            if (SelectedGridRow.Length >= 1)
            {
                Object Cell = SelectedGridRow[0]["PartnerKey"];

                if (Cell.GetType() == typeof(Int64))
                {
                    SupplierKey = Convert.ToInt64(Cell);
                }
            }

            return SupplierKey;
        }

        /// <summary>
        /// open the transactions of the selected supplier
        /// </summary>
        public void SupplierTransactions(object sender, EventArgs e)
        {
            Int64 SelectedSupplier = GetCurrentlySelectedSupplier();

            if (SelectedSupplier != -1)
            {
                TFrmAPSupplierTransactions frm = new TFrmAPSupplierTransactions(FMainForm);

                frm.LoadSupplier(FMainForm.LedgerNumber, SelectedSupplier);
                frm.Show();
            }
        }

        /// <summary>
        /// create a new supplier
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void NewSupplier(object sender, EventArgs e)
        {
            Int64 PartnerKey = -1;
            String ResultStringLbl;
            TPartnerClass? PartnerClass;
            TLocationPK ResultLocationPK;

            // the user has to select an existing partner to make that partner a supplier
            if (TPartnerFindScreenManager.OpenModalForm("",
                    TPartnerClass.ORGANISATION,
                    out PartnerKey,
                    out ResultStringLbl,
                    out PartnerClass,
                    out ResultLocationPK,
                    FMainForm))
            {
                TFrmAPEditSupplier frm = new TFrmAPEditSupplier(FMainForm);
                frm.LedgerNumber = FMainForm.LedgerNumber;
                frm.CreateNewSupplier(PartnerKey);
                frm.Show();
            }
        }

        /// <summary>
        /// edit an existing supplier
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void EditDetails(object sender, EventArgs e)
        {
            Int64 PartnerKey = GetCurrentlySelectedSupplier();

            if (PartnerKey != -1)
            {
                this.Cursor = Cursors.WaitCursor;
                TFrmAPEditSupplier frm = new TFrmAPEditSupplier(FMainForm);
                frm.LedgerNumber = FMainForm.LedgerNumber;
                frm.EditSupplier(PartnerKey);
                this.Cursor = Cursors.Default;

                frm.Show();
            }
        }

        /// <summary>
        /// create a new invoice
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void CreateInvoice(object sender, EventArgs e)
        {
            Int64 PartnerKey = GetCurrentlySelectedSupplier();

            if (PartnerKey != -1)
            {
                this.Cursor = Cursors.WaitCursor;
                TFrmAPEditDocument frm = new TFrmAPEditDocument(FMainForm);
                frm.CreateAApDocument(FMainForm.LedgerNumber, PartnerKey, false);
                this.Cursor = Cursors.Default;

                frm.Show();
            }
        }

        /// <summary>
        /// create a new credit note
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void CreateCreditNote(object sender, EventArgs e)
        {
            Int64 PartnerKey = GetCurrentlySelectedSupplier();

            if (PartnerKey != -1)
            {
                this.Cursor = Cursors.WaitCursor;
                TFrmAPEditDocument frm = new TFrmAPEditDocument(FMainForm);
                frm.CreateAApDocument(FMainForm.LedgerNumber, PartnerKey, true);
                this.Cursor = Cursors.Default;

                frm.Show();
            }
        }

        private void ApplyFilterManual(ref string AFilter)
        {
            if (FSupplierTable != null)
            {
                FSupplierTable.DefaultView.RowFilter = AFilter;

                bool gotRows = (grdDetails.Rows.Count > 1);

                ActionEnabledEvent(null, new ActionEventArgs("actEditDetails", gotRows));
                ActionEnabledEvent(null, new ActionEventArgs("actSupplierTransactions", gotRows));
                ActionEnabledEvent(null, new ActionEventArgs("actCreateInvoice", gotRows));
                ActionEnabledEvent(null, new ActionEventArgs("actCreateCreditNote", gotRows));

                FMainForm.ActionEnabledEvent(null, new ActionEventArgs("actEditDetails", gotRows));
                FMainForm.ActionEnabledEvent(null, new ActionEventArgs("actSupplierTransactions", gotRows));
                FMainForm.ActionEnabledEvent(null, new ActionEventArgs("actCreateInvoice", gotRows));
                FMainForm.ActionEnabledEvent(null, new ActionEventArgs("actCreateCreditNote", gotRows));

                if (gotRows)
                {
                    grdDetails.Selection.SelectRow(1, true);
                }
            }
        }

        private bool IsMatchingRowManual(DataRow ARow)
        {
            string supplierKey = ((TextBox)FFilterAndFindObject.FindPanelControls.FindControlByName("txtSupplierKey")).Text;

            if (supplierKey != String.Empty)
            {
                if (!ARow[0].ToString().Contains(supplierKey))
                {
                    return false;
                }
            }

            string supplierName = ((TextBox)FFilterAndFindObject.FindPanelControls.FindControlByName("txtSupplierName")).Text.ToLower();

            if (supplierName != String.Empty)
            {
                if (!ARow[1].ToString().ToLower().Contains(supplierName))
                {
                    return false;
                }
            }

            return true;
        }

        private bool ProcessCmdKeyManual(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.L | Keys.Control))
            {
                grdDetails.Focus();
                return true;
            }

            if (keyData == (Keys.Home | Keys.Control))
            {
                SelectRowInGrid(1);
                return true;
            }

            if (keyData == ((Keys.Up | Keys.Control)))
            {
                SelectRowInGrid(FPrevRowChangedRow - 1);
                return true;
            }

            if (keyData == (Keys.Down | Keys.Control))
            {
                SelectRowInGrid(FPrevRowChangedRow + 1);
                return true;
            }

            if (keyData == ((Keys.End | Keys.Control)))
            {
                SelectRowInGrid(grdDetails.Rows.Count - 1);
                return true;
            }

            return false;
        }
    }
}