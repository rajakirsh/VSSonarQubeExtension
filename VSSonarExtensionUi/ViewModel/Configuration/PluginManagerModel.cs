// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PluginManagerModel.cs" company="">
//   
// </copyright>
// <summary>
//   The dummy options controller.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace VSSonarExtensionUi.ViewModel.Configuration
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Windows.Forms;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Linq;

    using ExtensionTypes;

    using GalaSoft.MvvmLight.Command;

    using PropertyChanged;

    using SonarRestService;

    using VSSonarExtensionUi.View.Helpers;
    using VSSonarExtensionUi.ViewModel.Helpers;

    using VSSonarPlugins;
    using SQPluginManager;

    using Application = System.Windows.Application;
    using UserControl = System.Windows.Controls.UserControl;

    using Microsoft.FSharp.Collections;

    /// <summary>
    ///     The dummy options controller.
    /// </summary>
    [ImplementPropertyChanged]
    public class PluginManagerModel : IViewModelBase, IOptionsViewModelBase
    {
        #region Fields

        /// <summary>
        ///     The controller.
        /// </summary>
        private readonly ISQPluginManager controller;

        /// <summary>
        ///     The plugin list.
        /// </summary>
        private readonly ObservableCollection<PluginDescription> pluginList = new ObservableCollection<PluginDescription>();

        /// <summary>
        ///     The viewModel.
        /// </summary>
        private readonly VSonarQubeOptionsViewModel viewModel;

        private readonly SonarQubeViewModel sonarQubeViewModel;

        /// <summary>
        ///     The visual studio helper.
        /// </summary>
        private IVsEnvironmentHelper visualStudioHelper;

        private IConfigurationHelper configurationHelper;       
       
        #endregion

        #region Constructors and Destructors

        /// <summary>
        ///     Initializes a new instance of the <see cref="PluginManagerModel" /> class.
        /// </summary>
        public PluginManagerModel()
        {
            this.Header = "Plugin Manager";

            this.InitCommanding();

            this.ForeGroundColor = Colors.Black;
            this.ForeGroundColor = Colors.Black;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginManagerModel"/> class.
        /// </summary>
        /// <param name="controller">
        /// The controller.
        /// </param>
        /// <param name="conf">
        /// The conf.
        /// </param>
        /// <param name="visualStudioHelper">
        /// The visual Studio Helper.
        /// </param>
        /// <param name="viewModel">
        /// The viewModel.
        /// </param>
        public PluginManagerModel(
            ISQPluginManager controller, 
            ISonarConfiguration conf, 
            IVsEnvironmentHelper visualStudioHelper, 
            IConfigurationHelper configurationHelper,
            VSonarQubeOptionsViewModel viewModel,
            SonarQubeViewModel sonarQubeViewModel)
        {
            this.visualStudioHelper = visualStudioHelper;
            this.configurationHelper = configurationHelper;
            this.sonarQubeViewModel = sonarQubeViewModel;
            this.viewModel = viewModel;
            this.Header = "Plugin Manager";
            this.Conf = conf;
            this.controller = controller;

            this.InitPluginList();
            this.InitCommanding();
        }

        #endregion

        #region Public Properties

        public List<AnalysisPluginHolder> AnalysisPlugins { get; private set; }
        public List<MenuPluginHolder> MenuPlugins { get; private set; }

        /// <summary>
        ///     Gets the assembly directory.
        /// </summary>
        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                var uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        /// <summary>
        ///     Gets or sets the back ground color.
        /// </summary>
        public Color BackGroundColor { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether can modify plugin props.
        /// </summary>
        public bool CanModifyPluginProps { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether changes are required.
        /// </summary>
        public bool ChangesAreRequired { get; set; }

        /// <summary>
        ///     Gets or sets the conf.
        /// </summary>
        public ISonarConfiguration Conf { get; set; }

        /// <summary>
        ///     Gets or sets the excluded plugins.
        /// </summary>
        public string ExcludedPlugins { get; set; }

        /// <summary>
        ///     Gets or sets the fore ground color.
        /// </summary>
        public Color ForeGroundColor { get; set; }

        /// <summary>
        ///     Gets or sets the header.
        /// </summary>
        public string Header { get; set; }

        /// <summary>
        ///     Gets or sets the install new plugin.
        /// </summary>
        public RelayCommand InstallNewPlugin { get; set; }

        /// <summary>
        ///     Gets or sets the options in view.
        /// </summary>
        public UserControl OptionsInView { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether plugin is selected.
        /// </summary>
        public bool PluginIsSelected { get; set; }

        /// <summary>
        ///     Gets the plugin list.
        /// </summary>
        public ObservableCollection<PluginDescription> PluginList
        {
            get
            {
                return this.pluginList;
            }
        }

        /// <summary>
        ///     Gets or sets the remove plugin.
        /// </summary>
        public RelayCommand RemovePlugin { get; set; }

        /// <summary>
        ///     Gets or sets the restart vs.
        /// </summary>
        public RelayCommand RestartVs { get; set; }

        /// <summary>
        ///     Gets or sets the selected plugin.
        /// </summary>
        public PluginDescription SelectedPlugin { get; set; }

        /// <summary>
        ///     Gets or sets the selection changed command.
        /// </summary>
        public ICommand SelectionChangedCommand { get; set; }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        ///     The apply.
        /// </summary>
        public void Apply()
        {
            if (this.AnalysisPlugins != null)
            {
                foreach (var plugin in this.AnalysisPlugins)
                {
                    this.configurationHelper.WriteOptionInApplicationData(
                        GlobalIds.PluginEnabledControlId,
                        plugin.Plugin.GetPluginDescription(this.configurationHelper).Name,
                        plugin.Plugin.GetPluginDescription(this.configurationHelper).Enabled.ToString());

                    if (this.viewModel.Project != null)
                    {
                        try
                        {
                            Dictionary<string, string> options = plugin.Plugin.GetPluginControlOptions(this.Conf).GetOptions();
                            string key = plugin.Plugin.GetKey(this.Conf);
                            this.configurationHelper.WriteAllOptionsForPluginOptionInApplicationData(key, this.viewModel.Project, options);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     The end data association.
        /// </summary>
        public void EndDataAssociation()
        {
            this.CanModifyPluginProps = false;
        }

        /// <summary>
        ///     The exit.
        /// </summary>
        public void Exit()
        {
            if (this.AnalysisPlugins == null)
            {
                return;
            }

            foreach (IAnalysisPlugin plugin in this.AnalysisPlugins)
            {
                if (plugin.GetPluginControlOptions(this.Conf) == null)
                {
                    continue;
                }

                bool fileIsMissing = File.Exists(this.configurationHelper.UserAppDataConfigurationFile());

                if (!fileIsMissing || this.configurationHelper.ReadAllAvailableOptionsInSettings(plugin.GetKey(this.Conf)).Count == 0)
                {
                    plugin.GetPluginControlOptions(this.Conf).ResetDefaults();

                    string pluginKey = plugin.GetKey(this.Conf);
                    Dictionary<string, string> optionsToSave = plugin.GetPluginControlOptions(this.Conf).GetOptions();
                    this.configurationHelper.WriteAllOptionsForPluginOptionInApplicationData(pluginKey, null, optionsToSave);
                }
            }
        }

        /// <summary>
        /// The init data association.
        /// </summary>
        /// <param name="associatedProject">
        /// The associated project.
        /// </param>
        /// <param name="userConnectionConfig">
        /// The user connection config.
        /// </param>
        /// <param name="workingDir">
        /// The working dir.
        /// </param>
        public void InitDataAssociation(Resource associatedProject, ISonarConfiguration userConnectionConfig, string workingDir)
        {
        }

        /// <summary>
        ///     Gets or sets a value indicating whether install new plugin.
        /// </summary>
        public void OnInstallNewPlugin()
        {
            var filedialog = new OpenFileDialog { Filter = @"VSSonar Plugin|*.VSQ" };
            DialogResult result = filedialog.ShowDialog();

            if (result == DialogResult.OK)
            {
                if (!File.Exists(filedialog.FileName))
                {
                    UserExceptionMessageBox.ShowException("Error Choosing File, File Does not exits", null);
                }
                else
                {
                    try
                    {
                        var analysisPlugins = this.controller.InstallAnalysisPlugin(filedialog.FileName, ListModule.OfSeq(this.AnalysisPlugins));
                        var menuPlugins = this.controller.InstallMenuPlugin(filedialog.FileName, ListModule.OfSeq(this.MenuPlugins));

                        foreach (var plugin in menuPlugins)
                        {
                            var description = plugin.Plugin.GetPluginDescription(this.configurationHelper);
                            if (this.PluginList.SingleOrDefault(c => c.Name.Equals(description.Name)) == null)
                            {
                                this.PluginList.Add(description);
                                this.MenuPlugins.Add(plugin);
                                this.sonarQubeViewModel.AddANewMenu(plugin);
                            }                            
                        }

                        foreach (var plugin in analysisPlugins)
                        {
                            var description = plugin.Plugin.GetPluginDescription(this.configurationHelper);
                            if (this.PluginList.SingleOrDefault(c => c.Name.Equals(description.Name)) == null)
                            {
                                this.PluginList.Add(description);
                                this.AnalysisPlugins.Add(plugin);
                            }   
                        }                        
                    }
                    catch (Exception ex)
                    {
                        if (this.controller.InstallErrors().Length > 0)
                        {
                            UserExceptionMessageBox.ShowException(
                                "Cannot Install Plugin",
                                new Exception("Error Loading Plugin"),
                                SeqModule.OfList(this.controller.InstallErrors()).Aggregate((current, next) => current + "\r\n" + next));
                        }
                        else
                        {
                            UserExceptionMessageBox.ShowException(
                                "Cannot Install Plugin",
                                ex);
                        }

                    }
                }
            }
        }

        /// <summary>
        ///     Gets or sets a value indicating whether remove plugin.
        /// </summary>
        public void OnRemovePlugin()
        {
            var plugin = this.AnalysisPlugins.SingleOrDefault(s => s.Plugin.GetPluginDescription(this.configurationHelper).Name.Equals(this.SelectedPlugin.Name));
            if(plugin != null)
            {
                this.controller.DeleteAnalysisPlugin(plugin, ListModule.OfSeq(this.AnalysisPlugins));
                this.AnalysisPlugins.Remove(plugin);
                this.PluginList.Remove(this.SelectedPlugin);
                return;
            }

            var menuPlugin = this.MenuPlugins.SingleOrDefault(s => s.Plugin.GetPluginDescription(this.configurationHelper).Name.Equals(this.SelectedPlugin.Name));
            if (menuPlugin != null)
            {
                this.controller.DeleteMenuPlugin(menuPlugin, ListModule.OfSeq(this.MenuPlugins));
                this.MenuPlugins.Remove(menuPlugin);
                this.PluginList.Remove(this.SelectedPlugin);
                this.sonarQubeViewModel.RemoveMenuPlugin(menuPlugin);
            }
        }

        /// <summary>
        ///     Gets or sets a value indicating whether restart vs.
        /// </summary>
        public void OnRestartVs()
        {
            try
            {
                if (this.visualStudioHelper.AreWeRunningInVisualStudio())
                {
                    this.visualStudioHelper.RestartVisualStudio();
                }
                else
                {
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Application.Current.Shutdown();
            }

        }

        /// <summary>
        ///     The save and close.
        /// </summary>
        public void SaveAndClose()
        {
            this.Apply();
        }

        /// <summary>
        /// The update colours.
        /// </summary>
        /// <param name="background">
        /// The background.
        /// </param>
        /// <param name="foreground">
        /// The foreground.
        /// </param>
        public void UpdateColours(Color background, Color foreground)
        {
            this.BackGroundColor = background;
            this.ForeGroundColor = foreground;

            foreach (var menuPlugin in this.MenuPlugins)
            {
                menuPlugin.Plugin.UpdateTheme(background, foreground);
            }
        }

        /// <summary>
        /// The update services.
        /// </summary>
        /// <param name="restServiceIn">
        /// The rest service in.
        /// </param>
        /// <param name="vsenvironmenthelperIn">
        /// The vsenvironmenthelper in.
        /// </param>
        /// <param name="statusBar">
        /// The status bar.
        /// </param>
        /// <param name="provider">
        /// The provider.
        /// </param>
        public void UpdateServices(
            ISonarRestService restServiceIn, 
            IVsEnvironmentHelper vsenvironmenthelperIn, 
            IConfigurationHelper configurationHelper,
            IVSSStatusBar statusBar, 
            IServiceProvider provider)
        {
            this.configurationHelper = configurationHelper;
            this.visualStudioHelper = vsenvironmenthelperIn;
        }

        #endregion

        #region Methods

        /// <summary>
        ///     The init commanding.
        /// </summary>
        private void InitCommanding()
        {
            this.InstallNewPlugin = new RelayCommand(this.OnInstallNewPlugin);
            this.RestartVs = new RelayCommand(this.OnRestartVs);
            this.RemovePlugin = new RelayCommand(this.OnRemovePlugin);

            this.SelectionChangedCommand = new RelayCommand(this.OnSelectionChangeCommand);
        }

        /// <summary>
        ///     The init plugin list.
        /// </summary>
        private void InitPluginList()
        {
            this.AnalysisPlugins = SeqModule.OfList(this.controller.LoadAnalysisPlugins()).ToList();
            this.MenuPlugins = SeqModule.OfList(this.controller.LoadMenuPlugins()).ToList();

            foreach (var plugin in this.AnalysisPlugins)
            {
                this.PluginList.Add(plugin.Plugin.GetPluginDescription(this.configurationHelper));
            }

            foreach (var plugin in this.MenuPlugins)
            {
                this.PluginList.Add(plugin.Plugin.GetPluginDescription(this.configurationHelper));
            }
        }

        /// <summary>
        ///     The on selection change command.
        /// </summary>
        private void OnSelectionChangeCommand()
        {
            if (this.SelectedPlugin != null)
            {
                this.PluginIsSelected = true;

                foreach (var plugin in this.AnalysisPlugins)
                {
                    if (plugin.Plugin.GetPluginDescription(this.configurationHelper).Name.Equals(this.SelectedPlugin.Name))
                    {
                        try
                        {
                            this.OptionsInView = plugin.Plugin.GetPluginControlOptions(this.Conf).GetUserControlOptions(this.viewModel.Project);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }

                        this.CanModifyPluginProps = this.viewModel.Project != null;

                        return;
                    }
                }
            }
            else
            {
                this.PluginIsSelected = false;
                this.OptionsInView = null;
            }
        }

        /// <summary>
        /// The update plugin in list of plugins.
        /// </summary>
        /// <param name="plugin">
        /// The plugin.
        /// </param>
        /// <param name="pluginWillBeRemovedInNextRestart">
        /// The plugin will be removed in next restart.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        private bool UpdatePluginInListOfPlugins(PluginDescription plugin, string pluginWillBeRemovedInNextRestart)
        {
            bool present = false;
            var newList = new ObservableCollection<PluginDescription>();
            foreach (PluginDescription pluginDescription in this.PluginList)
            {
                if (pluginDescription.Name.Equals(plugin.Name))
                {
                    var updatedDescription = new PluginDescription
                                                 {
                                                     Name = pluginDescription.Name, 
                                                     Enabled = pluginDescription.Enabled, 
                                                     Status = pluginWillBeRemovedInNextRestart, 
                                                     SupportedExtensions = pluginDescription.SupportedExtensions, 
                                                     Version = pluginDescription.Version
                                                 };

                    newList.Add(updatedDescription);
                    present = true;
                }
                else
                {
                    newList.Add(pluginDescription);
                }
            }

            this.PluginList.Clear();
            foreach (PluginDescription pluginDescription in newList)
            {
                this.PluginList.Add(pluginDescription);
            }

            return present;
        }

        #endregion
    }
}