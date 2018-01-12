using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Windows;

namespace LS.MapClean.Addin.Palettes
{
    abstract class PaletteSetBase
    {
        const string _applicationName = "MapClean";

        /// <summary>
        /// Default palette set style; subclasses can modify in their ctor.
        /// </summary>
        private PaletteSetStyles m_paletteSetStyle =
            PaletteSetStyles.ShowPropertiesMenu
            | PaletteSetStyles.ShowAutoHideButton
            | PaletteSetStyles.ShowCloseButton
            | PaletteSetStyles.UsePaletteNameAsTitleForSingle;
        protected PaletteSetStyles PaletteSetStyle
        {
            get { return m_paletteSetStyle; }
            set { m_paletteSetStyle = value; }
        }

        #region PaletteSet related properties.
        /// <summary>
        /// The underlying AutoCAD PaletteSet object.
        /// </summary>
        public PaletteSet PaletteSet { get; private set; }
        /// <summary>
        /// The AutoCAD Palette object corresponding to the first/default palette in the set.
        /// </summary>
        public Palette Palette { get; private set; }

        /// <summary>
        /// Palette set type.
        /// </summary>
        public PaletteSetType PaletteSetType { get; protected set; }
        /// <summary>
        /// Palette set's internal name.
        /// </summary>
        public string InternalName { get; protected set; }

        /// <summary>
        /// Palette set's display name.
        /// </summary>
        private string _displayName = String.Empty;
        public string DisplayName
        {
            get { return _displayName; }
            set
            {
                _displayName = value;
                if (PaletteSet != null && PaletteSet.Count > 0)
                {
                    PaletteSet[0].Name = _displayName;
                }
            }
        }

        /// <summary>
        /// If it's not empty, used as palette set's id for persistence.
        /// </summary>
        public Guid PaletteSetId { get; protected set; }

        /// <summary>
        /// PaletteSet's initial dock state.
        /// </summary>
        public DockSides InitialDockState { get; protected set; }

        /// <summary>
        /// If not-null, will be used as the name of system variable to hold the state of PaletteSet.
        /// </summary>
        public string StateVariableName
        {
            get { return "MC" + InternalName + "State"; }
        }

        // Whether the palette set is initialized
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Whether save visible state.
        /// </summary>
        private bool _saveVisibleState = true;
        public bool SaveVisibleState
        {
            get { return _saveVisibleState; }
            set { _saveVisibleState = value; }
        }
        #endregion

        #region Methods
        public bool HasVisibleState()
        {
            bool result = false;  // Default state

            using (IConfigurationSection globalcs = Application.UserConfigurationManager.OpenCurrentProfile())
            {
                if (globalcs.ContainsSubsection(_applicationName))
                {
                    using (IConfigurationSection thiscs = globalcs.OpenSubsection(_applicationName))
                    {
                        if (thiscs.Contains(StateVariableName))
                            result = (int)thiscs.ReadProperty(StateVariableName, 0) == 1;
                    }
                }
            }

            return result;
        }
        #endregion

        #region Virtual methods
        /// <summary>
        /// Subclass must override it to create the control that will be showed in default palette.
        /// </summary>
        /// <returns></returns>
        protected abstract System.Windows.Forms.Control CreateDefaultPaletteControl();

        /// <summary>
        /// Subclasses may override to do additional initialization after palette set is created.
        /// Default implementation does nothing.
        /// </summary>
        protected virtual void OnPaletteCreated()
        {
        }

        /// <summary>
        ///  Subclasses may override to do additional initialization after palette is activated.
        /// </summary>
        protected virtual void OnPaletteActivated(PaletteActivatedEventArgs e)
        {
        }

        /// <summary>
        /// Subclasses may override to do any necessary processing to refresh the palette set's contents.
        /// </summary>
        protected virtual void OnRefresh(Document document)
        {
        }


        protected virtual void OnPaletteVisibilityChanged(bool visible)
        {
            if (VisibilityChanged != null)
            { 
                VisibilityChanged(this, new PaletteVisibleEventArgs(){IsVisible = visible});
            }
        }
        #endregion

        #region Operations
        public void Show(Document document)
        {
            if (!IsInitialized)
                CreatePalette(document);
            PaletteSet.Visible = true;
        }

        // This will be set to true inside Hide(), if "temporary" is True.
        // This indicates we don't want to record, in the State Variable, that the window is being closed.
        // That's reserved for user actions
        private static bool _skipVariableUpdate = false;

        public void Hide(bool temporary = false)
        {
            if (!IsInitialized)
                return;

            if (temporary)
                _skipVariableUpdate = true;

            PaletteSet.Visible = false;

            _skipVariableUpdate = false;
        }

        private void CreatePalette(Document document)
        {
            // Won't create anything in zero-doc mode.
            if (document == null)
                throw new InvalidOperationException();

            if (IsInitialized)
                return;

            // Create
            if (PaletteSetId == Guid.Empty)
                PaletteSet = new PaletteSet(DisplayName);
            else
                PaletteSet = new PaletteSet(DisplayName, PaletteSetId);

            // Set style
            PaletteSet.Style = PaletteSetStyle;

            // Initial dock state
            PaletteSet.Dock = InitialDockState;

            // Create/add the default palette
            var control = CreateDefaultPaletteControl();
            Palette = PaletteSet.Add(DisplayName, control);

            // Register event handlers.
            PaletteSet.StateChanged += this.PaletteStateChanged;
            PaletteSet.PaletteActivated += this.PaletteActivated;

            // Call virtual methods
            OnPaletteCreated();

            // Set flag to true
            IsInitialized = true;
        }
        #endregion

        #region Event Handlers
        void PaletteActivated(object sender, PaletteActivatedEventArgs args)
        {
            OnPaletteActivated(args);
        }
        void PaletteStateChanged(object sender, PaletteSetStateEventArgs args)
        {
            if (String.IsNullOrEmpty(this.StateVariableName))
                return;
            switch (args.NewState)
            {
                case StateEventIndex.Show:
                    UpdateStateVariable(visible: true);
                    OnPaletteVisibilityChanged(visible: true);
                    break;
                case StateEventIndex.Hide:
                    if (!_skipVariableUpdate)
                        UpdateStateVariable(visible: false);
                    OnPaletteVisibilityChanged(visible:false);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Store a value into the current profile indicating the saved visible state
        /// </summary>
        /// <param name="visible"></param>
        void UpdateStateVariable(bool visible)
        {
            if (!SaveVisibleState)
                return;

            using (IConfigurationSection globalcs = Application.UserConfigurationManager.OpenCurrentProfile())
            {
                using (IConfigurationSection thiscs = globalcs.ContainsSubsection(_applicationName)
                                                ? globalcs.OpenSubsection(_applicationName)
                                                : globalcs.CreateSubsection(_applicationName))
                {
                    object newValue = visible ? (int)1 : (int)0;
                    thiscs.WriteProperty(StateVariableName, newValue);
                }
            }
        }
        #endregion

        #region Events

        public EventHandler<PaletteVisibleEventArgs> VisibilityChanged;

        #endregion
    }

    public class PaletteVisibleEventArgs : EventArgs
    {
        public bool IsVisible { get; set; }
    }
}
