﻿using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.IntelliSense.Navigation.NavigationList
{
    public partial class NavigationListControl : UserControl
    {
        public NavigationListControl()
        {
            InitializeComponent();
        }

        public async Task UpdateNavigationListAsync(IEnumerable<NavigationToken> navitaionList)
        {
            var navigationGroups = navitaionList.GroupBy(n => n.Path).ToList();

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            NavigationTokens.Items.Clear();
            foreach (var group in navigationGroups)
            {
                var groupItems = new NavigationListNode(group.Key);
                foreach (var definitionItem in group)
                {
                    groupItems.Items.Add(new NavigationListItemNode(definitionItem));
                }

                NavigationTokens.Items.Add(groupItems);
            }
        }

        private void NavigationTokens_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
            NavigateToToken();

        private void NavigationTokens_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                NavigateToToken();
            }
        }

        private void NavigateToToken()
        {
            if (NavigationTokens.SelectedItem is NavigationListItemNode item)
                item.NavigationToken.Navigate();
        }
    }
}