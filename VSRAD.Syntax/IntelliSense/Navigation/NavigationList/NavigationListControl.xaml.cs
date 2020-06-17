using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using VSRAD.Syntax.Helpers;
using Task = System.Threading.Tasks.Task;

namespace VSRAD.Syntax.IntelliSense.Navigation.NavigationList
{
    public partial class NavigationListControl : UserControl
    {
        private readonly INavigationTokenService _navigationTokenService;

        public NavigationListControl(INavigationTokenService navigationTokenService)
        {
            _navigationTokenService = navigationTokenService;
            InitializeComponent();
        }

        public async Task UpdateNavigationListAsync(IEnumerable<NavigationToken> navitaionList)
        {
            var definitionGroups = navitaionList
                .Select(n => new DefinitionToken(n))
                .GroupBy(d => d.FilePath)
                .ToList();

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            NavigationTokens.Items.Clear();
            foreach (var group in definitionGroups)
            {
                var navigationListGroupItem = new NavigationListItem(group.Key);
                foreach (var definitionItem in group)
                {
                    navigationListGroupItem.Items.Add(new NavigationListDefinitionItem(definitionItem));
                }

                NavigationTokens.Items.Add(navigationListGroupItem);
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
            try
            {
                if (NavigationTokens.SelectedItem is NavigationListDefinitionItem item)
                    _navigationTokenService.GoToPoint(item.DefinitionToken.NavigationToken);
            }
            catch (Exception e)
            {
                Error.LogError(e, "Navigation list");
            }
        }
    }
}