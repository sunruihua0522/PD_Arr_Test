using System.Collections.Specialized;
using System.Windows.Controls;

namespace PLC_Test_PD_Array.UserControls
{
    public class AutoScrollingListBox : ListBox
    {
        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {

                int newItemCount = e.NewItems.Count;

                if (newItemCount > 0)
                    this.ScrollIntoView(e.NewItems[newItemCount - 1]);
            }

            base.OnItemsChanged(e);
        }
    }
}
