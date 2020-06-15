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
            var definitionlist = navitaionList
                .Select(n => new DefinitionToken(n))
                .ToList();

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            navigationTokens.Items.Clear();
            foreach (var token in definitionlist)
            {
                navigationTokens.Items.Add(token);
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
                var navigationToken = (DefinitionToken)navigationTokens.SelectedItem;
                _navigationTokenService.GoToPoint(navigationToken.NavigationToken);
            }
            catch (Exception e)
            {
                Error.LogError(e, "Navigation list");
            }
        }
    }
}