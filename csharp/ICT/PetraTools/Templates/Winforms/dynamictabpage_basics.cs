{##DYNAMICTABPAGE}
private void OnTabPageEvent(TTabPageEventArgs e)
{
    if (FTabPageEvent != null)
    {
        FTabPageEvent(this, e);
    }
}

{#IFDEF ISUSERCONTROL}
private void OnDataLoadingFinished()
{
    if (DataLoadingFinished != null)
    {
        DataLoadingFinished(this, new EventArgs());
    }
}

private void OnDataLoadingStarted()
{
    if (DataLoadingStarted != null)
    {
        DataLoadingStarted(this, new EventArgs());
    }
}

/// Dynamically loads UserControls that are associated with the Tabs. AUTO-GENERATED, don't modify by hand!
/// </summary>
/// <param name="sender"></param>
/// <param name="e"></param>
private void TabSelectionChanged(System.Object sender, EventArgs e)
{
    //MessageBox.Show("TabSelectionChanged. Current Tab: " + tabPartners.SelectedTab.ToString());

    if (FTabSetup == null)
    {
        FTabSetup = new SortedList<TDynamicLoadableUserControls, UserControl>();

        // The first time we run this Method we exit straight away; this is when the Form gets initialised        
        return;
    }

    {#DYNAMICTABPAGEUSERCONTROLSELECTIONCHANGED}
}
{#ENDIF ISUSERCONTROL}

/// <summary>
/// Creates UserControls on request. AUTO-GENERATED, don't modify by hand!
/// </summary>
/// <param name="AUserControl">UserControl to load.</param>
private UserControl DynamicLoadUserControl(TDynamicLoadableUserControls AUserControl)
{
    UserControl ReturnValue = null;

    switch (AUserControl)
    {
        {#DYNAMICTABPAGEUSERCONTROLLOADING}
    }

    return ReturnValue;
}