using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using LS.MapClean.Addin.Algorithms;
using LS.MapClean.Addin.Framework;
using LS.MapClean.Addin.MapClean.Actions;
using LS.MapClean.Addin.MapClean.Actions2;
using LS.MapClean.Addin.Palettes;
using LS.MapClean.Addin.Settings;
using LS.MapClean.Addin.Utils;
using LS.MapClean.Addin.View;
using LS.MapClean.Addin.ViewModel;
using TopologyTools.Utils;
using AcadColor = Autodesk.AutoCAD.Colors.Color;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace LS.MapClean.Addin.MapClean
{
    public class MapCleanService
    {
        #region Singleton
        private static volatile MapCleanService _instance;
        private static object _syncRoot = new object();
        public static MapCleanService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_syncRoot)
                    {
                        // Double check
                        if (_instance == null)
                            _instance = new MapCleanService();
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Constructors

        public MapCleanService()
        {
            // Register document events
            RegisterDocumentEvents();
            // Register paleteset events
            RegisterPalettesetEvents();
        }

        private void InitializeCleanActionAgents()
        {
            _actionAgents.Clear();

            // Break crossing objects.
            AddActionAgent(ActionType.BreakCrossing, new ActionType[0], ActionStatus.Pending);

            // Delete duplicate entities
            AddActionAgent(ActionType.DeleteDuplicates, new ActionType[] { ActionType.BreakCrossing }, ActionStatus.Disabled);
            
            // Extend under shoots
            AddActionAgent(ActionType.ExtendUndershoots, new ActionType[] { ActionType.DeleteDuplicates }, ActionStatus.Disabled);
            
            // Apparent intersection
            AddActionAgent(ActionType.ApparentIntersection, new ActionType[] { ActionType.ExtendUndershoots }, ActionStatus.Disabled);
            
            // Snap clustered
            AddActionAgent(ActionType.SnapClustered, new ActionType[] { ActionType.ApparentIntersection }, ActionStatus.Disabled);

            // Erase dangling
            AddActionAgent(ActionType.EraseDangling, new ActionType[] { ActionType.SnapClustered }, ActionStatus.Disabled);

            // Zero area loop
            AddActionAgent(ActionType.ZeroAreaLoop, new ActionType[] { ActionType.EraseDangling }, ActionStatus.Disabled);

            // Zero length
            AddActionAgent(ActionType.ZeroLength, new ActionType[] { ActionType.ZeroAreaLoop }, ActionStatus.Disabled);

            // Erase short
            AddActionAgent(ActionType.EraseShort, new ActionType[] { ActionType.ZeroLength }, ActionStatus.Disabled);

            foreach (var pair in _actionAgents)
            {
                pair.Value.StatusChanged += OnActionStatusChanged;
            }
        }

        private void InitializePolygonActionAgents()
        {
            _actionAgents.Clear();

            // Unclosed polygon
            AddActionAgent(ActionType.UnclosedPolygon, new ActionType[0], ActionStatus.Pending);

            // Self intersect
            AddActionAgent(ActionType.SelfIntersect, new ActionType[] { ActionType.UnclosedPolygon }, ActionStatus.Disabled);

            // Small polygon
            AddActionAgent(ActionType.SmallPolygon, new ActionType[] { ActionType.SelfIntersect }, ActionStatus.Disabled);

            // Intersect polygon
            AddActionAgent(ActionType.IntersectPolygon, new ActionType[] { ActionType.SmallPolygon }, ActionStatus.Disabled);

            // Small gap
            AddActionAgent(ActionType.SmallPolygonGap, new ActionType[] { ActionType.IntersectPolygon }, ActionStatus.Disabled);

            AddActionAgent(ActionType.MissingVertexInPolygon, new ActionType[] { ActionType.MissingVertexInPolygon }, ActionStatus.Disabled);
            AddActionAgent(ActionType.SelfIntersect2, new ActionType[] { ActionType.SelfIntersect2 }, ActionStatus.Disabled);
            AddActionAgent(ActionType.FindDangling, new ActionType[] { ActionType.FindDangling }, ActionStatus.Disabled);
            AddActionAgent(ActionType.OverlapPolygon, new ActionType[] { ActionType.OverlapPolygon }, ActionStatus.Disabled);


            foreach (var pair in _actionAgents)
            {
                pair.Value.StatusChanged += OnActionStatusChanged;
            }
        }

        private void AddActionAgent(ActionType actionType, ActionType[] dependencies, ActionStatus status)
        {
            var agent = new ActionAgent()
            {
                ActionType = actionType,
                Dependencies = dependencies,
                Status = status,
                Name = actionType.ToChineseName(),
                Action = CreateAction(actionType)
            };
            _actionAgents.Add(actionType, agent);
        }

        private MapCleanActionBase CreateAction(ActionType actionType)
        {
            MapCleanActionBase action = null;
            switch (actionType)
            {
                case ActionType.BreakCrossing:
                    action = new BreakCrossingObjectsAction(Document);
                    break;
                case ActionType.DeleteDuplicates:
                    action = new DuplicateEntitiesAction(Document);
                    break;
                case ActionType.ExtendUndershoots:
                    action = new ExtendUndershootsAction(Document);
                    break;
                case ActionType.ApparentIntersection:
                    action = new ApparentIntersectionAction(Document);
                    break;
                case ActionType.SnapClustered:
                    action = new SnapClusteredNodesAction(Document);
                    break;
                case ActionType.EraseDangling:
                    action = new DanglingObjectsAction(Document);
                    break;
                case ActionType.ZeroAreaLoop:
                    action = new ZeroAreaLoopAction(Document);
                    break;
                case ActionType.ZeroLength:
                    action = new ZeroLengthObjectsAction(Document);
                    break;
                case ActionType.EraseShort:
                    action = new ResolveShortLinesAction(Document);
                    break;

                case ActionType.AntiClockwisePolygon:
                    action = new AntiClockwisePolygonAction(Document);
                    break;
                case ActionType.UnclosedPolygon:
                    action = new UnclosedPolygonAction(Document);
                    break;
                case ActionType.SelfIntersect:
                    action = new SelfIntersectionAction(Document);
                    break;
                case ActionType.SmallPolygon:
                    action = new SmallPolygonAction(Document);
                    break;
                case ActionType.IntersectPolygon:
                    action = new IntersectPolygonAction(Document);
                    break;
                case ActionType.SmallPolygonGap:
                    action = new SmallPolygonGapAction(Document);
                    break;
                case ActionType.PolygonHole:
                    action = new PolygonHoleAction(Document);
                    break;
                case ActionType.MissingVertexInPolygon:
                    action = new MissingVertexInPolygonAction(Document);
                    break;
                case ActionType.OverlapPolygon:
                    action = new OverlapPolygonAction(Document);
                    break;
                case ActionType.SelfIntersect2:
                    action = new SelfIntersectionInPolygonAction(Document);
                    break;
                case ActionType.FindDangling:
                    action = new FindDanglingAction(Document);
                    break;
                case ActionType.AnnotationOverlap:
                    action = new AnnotationOverlapAction(Document);
                    break;
            }
            return action;
        }

        private void RegisterDocumentEvents()
        {
            var docManager = Application.DocumentManager;
            docManager.DocumentToBeDeactivated += OnDocumentToBeDeactivated;
            docManager.DocumentToBeActivated += OnDocumentToBeActivated;
            docManager.DocumentBecameCurrent += OnDocumentBecameCurrent;
            docManager.DocumentToBeDestroyed += OnDocumentToBeDestroyed;
        }

        private void RegisterPalettesetEvents()
        {
            var palette = AllPaletteSets.GetPaletteSet(PaletteSetType.MapClean);
            palette.VisibilityChanged += OnPanelVisibilityChanged;
            var actionPalette = AllPaletteSets.GetPaletteSet(PaletteSetType.ActionSequence);
            actionPalette.VisibilityChanged += OnActionPaletteVisibilityChanged;
        }

        #endregion

        #region Properties

        /// <summary>
        /// MapCleanService's related document.
        /// </summary>
        private Document _document = null;
        public Document Document 
        {
            get { return _document; }
            private set
            {
                if (_document == value)
                    return;
                // Remove old document's event handler
                if (_document != null)
                    _document.CommandWillStart -= OnCommandWillStart;
                _document = value;
                // Add new document's event handler
                if (_document != null)
                    _document.CommandWillStart += OnCommandWillStart;
            }
        }

        /// <summary>
        /// Current check result which is highlighted by MapCleanService.
        /// </summary>
        public CheckResult CurrentCheckResult { get; private set; }

        /// <summary>
        /// Transient graphics manager
        /// </summary>
        private TransientGraphicsMgr _transientGraphicsMgr = new TransientGraphicsMgr();

        /// <summary>
        /// Check result groups of MapCleanService.
        /// </summary>
        private Dictionary<ActionType, CheckResultGroup> _checkResultGroups = new Dictionary<ActionType, CheckResultGroup>();
        public Dictionary<ActionType, CheckResultGroup> CheckResultGroups
        {
            get { return _checkResultGroups; }
        }

        private Dictionary<ObjectId, List<CheckResult>> _checkResultIndices = new Dictionary<ObjectId, List<CheckResult>>();
        public Dictionary<ObjectId, List<CheckResult>> CheckResultIndices
        {
            get { return _checkResultIndices; }
        }

        /// <summary>
        /// Action agents of MapCleanService.
        /// </summary>
        private Dictionary<ActionType, ActionAgent> _actionAgents = new Dictionary<ActionType, ActionAgent>();
        public Dictionary<ActionType, ActionAgent> ActionAgents
        {
            get { return _actionAgents; }
        }

        /// <summary>
        /// Executing agents of MapCleanService.
        /// </summary>
        private HashSet<ActionType> _executingActions = new HashSet<ActionType>();

        /// <summary>
        /// ActionsSettingViewModel instance.
        /// </summary>
        private ActionsSettingViewModel _actionsSettingViewModel = null;
        public ActionsSettingViewModel ActionsSettingViewModel
        {
            get { return _actionsSettingViewModel; }
        }

        protected MapCleanPanelViewModel PanelViewModel
        {
            get
            {
                var panel = AllPaletteSets.GetPaletteSet<MapCleanPaletteSet>();
                return panel.ViewModel;
            }
        }

        protected ActionSequenceViewModel ActionSequenceViewModel
        {
            get
            {
                var panel = AllPaletteSets.GetPaletteSet<ActionSequencePaletteSet>();
                return panel.ViewModel;
            }
        }

        public bool ShowIntegrateCheckItem { get; private set; }

        /// <summary>
        /// Delegate to get check ObjectIds.
        /// </summary>
        public Func<Document, IEnumerable<ObjectId>> GetCheckObjectIds;
        /// <summary>
        /// Deletegate to post handle check objectIds
        /// </summary>
        public Action<Document, IEnumerable<ObjectId>> PostHandleAfterCheck;
        #endregion

        #region APIs
        /// <summary>
        /// Start mapclean workflow from zero.
        /// </summary>
        /// <param name="document"></param>
        public void StartMapClean(Document document)
        {
            // Call End() first
            End(clearTransient: document == Document);

            // Set MapCleanService singleton instance related document.
            Document = document;

            InitializeCleanActionAgents();
            
            // 创建MapClean View和ViewModel.
            _actionsSettingViewModel = new ActionsSettingViewModel(this);

            if (NewCheckWithDialog())
            { 
                // Show map clean panel
                ShowMapCleanPanel(show: true, recordState:true);
            }
        }

        public void StartMapCleanSequence(Document document)
        {
            // Call End() first
            End(clearTransient: document == Document);

            // Set MapCleanService singleton instance related document.
            Document = document;

            InitializeCleanActionAgents();

            // 创建MapClean View和ViewModel.
            _actionsSettingViewModel = new ActionsSettingViewModel(this);
            _actionsSettingViewModel.ActionSelectVM.BreakCrossingObjects = false;

            ShowIntegrateCheckItem = true;
            // Action sequence palette set
            var actionSequenceVM = ActionSequenceViewModel;
            if (actionSequenceVM != null)
            {
                actionSequenceVM.Refresh();
            }

            ShowActionPalette(show: true, recordState:true);

            var displayName = "图形清理";
            var sequencePanel = AllPaletteSets.GetPaletteSet<ActionSequencePaletteSet>();
            sequencePanel.DisplayName = displayName;
            var resultPanel = AllPaletteSets.GetPaletteSet<MapCleanPaletteSet>();
            resultPanel.DisplayName = displayName;
        }

        public void StartPolygonCheckSequence(Document document)
        {
            // Call End() first
            End(clearTransient: document == Document);

            // Set MapCleanService singleton instance related document.
            Document = document;

            InitializePolygonActionAgents();

            // 创建MapClean View和ViewModel.
            _actionsSettingViewModel = new ActionsSettingViewModel(this);
            _actionsSettingViewModel.ActionSelectVM.BreakCrossingObjects = false;

            ShowIntegrateCheckItem = false;
            // Action sequence palette set
            var actionSequenceVM = ActionSequenceViewModel;
            if (actionSequenceVM != null)
            {
                actionSequenceVM.Refresh();
            }

            ShowActionPalette(show: true, recordState: true);
            var displayName = "多边形拓扑检查";
            var sequencePanel = AllPaletteSets.GetPaletteSet<ActionSequencePaletteSet>();
            sequencePanel.DisplayName = displayName;
            var resultPanel = AllPaletteSets.GetPaletteSet<MapCleanPaletteSet>();
            resultPanel.DisplayName = displayName;
        }

        public void StartPolygonCheckConsole(Document document, ActionType actionType)
        {
            // Call End() first
            End(clearTransient: document == Document, hideResultPanel: false);

            // Set MapCleanService singleton instance related document.
            Document = document;

            // Intialize action agents
            _actionAgents.Clear();
            AddActionAgent(actionType, new ActionType[0], ActionStatus.Pending);
            var resultPanel = AllPaletteSets.GetPaletteSet<MapCleanPaletteSet>();

            var displayName = "多边形拓扑检查";
            resultPanel.DisplayName = displayName;
        }

        /// <summary>
        /// End map clean workflow.
        /// </summary>
        /// <param name="clearTransient"></param>
        public void End(bool clearTransient = true, bool hideResultPanel = true)
        {
            // Hide map clean panel
            if (hideResultPanel)
            {
                ShowMapCleanPanel(show: false, recordState: false);
            }
            ShowActionPalette(show: false, recordState: false);

            if (clearTransient && CurrentCheckResult != null)
                _transientGraphicsMgr.ClearTransientGraphics();

            _executingActions.Clear();
            _actionAgents.Clear();

            CurrentCheckResult = null;
            ClearCheckResults(raiseEvent: true);
            Document = null;
            GetCheckObjectIds = null;
            PostHandleAfterCheck = null;
        }

        public void SetExecutingActions(ActionType[] actionTypes)
        {
            _executingActions.Clear();
            foreach (var actionType in actionTypes)
            {
                _executingActions.Add(actionType);
            }
        }

        /// <summary>
        /// Highlight a check result.
        /// </summary>
        /// <param name="result"></param>
        public void HighlightCheckResult(CheckResult result)
        {
            // Set current check result.
            var previousCheckResult = CurrentCheckResult;
            CurrentCheckResult = result;

            using (var transaction = Document.Database.TransactionManager.StartTransaction())
            {
                if (previousCheckResult != null)
                {
                    HighlightCheckResultEntities(previousCheckResult, false, transaction);
                }

                if (result.HighlightEntity)
                {
                    HighlightCheckResultEntities(result, true, transaction);
                }
                transaction.Commit();
            }

            // Create transient graphics
            ShowTransientErrorMark(result);

            // Zoom the entities
            //var marksize = result.BaseSize;
            //var vector = new Vector3d(1, 1, 0).GetNormal()*marksize/2;
            //var position = result.Position;
            //var minPoint = position - vector;
            //var maxPoint = position + vector;
            //var extents = new Extents3d(minPoint, maxPoint);
            var extents = result.GeometricExtents;
            if (extents.HasValue)
                Document.Editor.ZoomToWin(extents.Value, factor:1.2);
        }

        public void ClearTransientGraphics()
        {
            _transientGraphicsMgr.ClearTransientGraphics();
            Document.Editor.UpdateScreen();
        }

        public void Check()
        {
            using (var docLock = Document.LockDocument())
            {
                // Clear all check results
                if (PanelViewModel != null)
                    PanelViewModel.CheckResultsVM.Clear();
                ClearCheckResults(raiseEvent: false);

                IEnumerable<ObjectId> selectedIds = null;
                if (GetCheckObjectIds != null)
                    selectedIds = GetCheckObjectIds(Document);
                else
                    selectedIds = GetAllPolylineObjectIds();
                if (selectedIds == null || !selectedIds.Any())
                {
                    Document.Editor.WriteMessage("\n没有可检查对象\n");
                    return;
                }

                // Check
                NewCheckWithPrompt(selectedIds, "拓扑错误");

                var agents = _actionAgents.Where(it => _executingActions.Contains(it.Key)).Select(it => it.Value);
                foreach (var agent in agents)
                {
                    agent.Status |= ActionStatus.Executed;
                }

                if (PostHandleAfterCheck != null)
                    PostHandleAfterCheck(Document, selectedIds);

                ShowMapCleanPanel(true, true);
            }
        }

        public void CheckAndFix()
        {
            //if (CurrentCheckResult != null)
            //{
            //    _transientGraphicsMgr.ClearTransientGraphics();
            //    CurrentCheckResult = null;
            //}

            // Clear all check results
            if (PanelViewModel != null)
                PanelViewModel.CheckResultsVM.Clear();
            ClearCheckResults(raiseEvent: false);

            var selectedIds = GetSelectedObjectIds();
            if (selectedIds == null || !selectedIds.Any())
            { 
                Document.Editor.WriteMessage("\n没有可检查对象\n");
                return;
            }

            var agents = _actionAgents.Where(it => _executingActions.Contains(it.Key)).Select(it => it.Value);
            foreach (var agent in agents)
            {
                agent.Action.CheckAndFixAll(selectedIds);
                agent.Status |= ActionStatus.Executed;
            }
        }

        /// <summary>
        /// Continue check by switching current settings
        /// </summary>
        public void ContinueCheck()
        {
            _actionsSettingViewModel.ActionSelectVM.SwitchSelectedActions();
            NewCheckWithDialog();
        }

        /// <summary>
        /// Re-check based on current settings.
        /// </summary>
        public void Recheck()
        {
            using (var docLock = Document.LockDocument())
            {
                Recheck(rejectedObjIds: null, objectName: "拓扑错误");
            }
            Document.Editor.WriteMessage("\n");
        }

        /// <summary>
        /// Fix a single check result.
        /// </summary>
        /// <param name="result"></param>
        /// <param name="updateScreen"></param>
        public void FixCheckResult(CheckResult result, bool updateScreen)
        {
            if (result.Status != Status.Pending)
                return;

            var agent = _actionAgents[result.ActionType];
            // Must lock document because it's not in a model command.
            using (var docLock = Document.LockDocument())
            {
                List<ObjectId> resultIds = null;
                result.Status = agent.Action.Fix(result, out resultIds);
                if (result.Status == Status.Fixed)
                {
                    result.TargetIds = resultIds.ToArray();

                    // Update some check results to be invalid if they contains same ObjectIds.
                    foreach (var sourceId in result.SourceIds)
                    {
                        var relatedResults = _checkResultIndices[sourceId];
                        foreach (var relatedResult in relatedResults)
                        {
                            if (relatedResult == result)
                                continue;

                            relatedResult.Status = Status.Invalid;
                        }
                    }

                    // Update screen
                    if (updateScreen)
                    {
                        Document.Editor.UpdateScreen();
                    }
                }
            }
        }

        /// <summary>
        /// Fix a list of check results.
        /// </summary>
        /// <param name="results"></param>
        /// <param name="updateScreen"></param>
        public void FixCheckResults(IEnumerable<CheckResult> results, bool updateScreen)
        {
            foreach (var checkResult in results)
            {
                FixCheckResult(checkResult, false);
            }
            if (updateScreen)
                Document.Editor.UpdateScreen();
        }

        /// <summary>
        /// Fix all check results.
        /// </summary>
        /// <param name="recursiveCheck"></param>
        /// <param name="verb"></param>
        /// <param name="objectName"></param>
        public void FixAllCheckResults(bool recursiveCheck, string verb, string objectName)
        {
            // Record all rejected object Ids.
            var rejectedObjIds = GetAllRejectedObjectIds();

            if (NeedContinueFixAllCheckResults())
            {
                FixAllCheckResultsWithProgresser(updateScreen: false, verb: verb, objectName: objectName);
            }

            if (recursiveCheck)
            {
                while (true)
                {
                    // Recheck
                    Recheck(rejectedObjIds, objectName);

                    if (!NeedContinueFixAllCheckResults())
                    {
                        Document.Editor.WriteMessage("，{0}成功！\n", verb);
                        break;
                    }

                    Document.Editor.WriteMessage("，继续{0}...", verb);
                    FixAllCheckResultsWithProgresser(updateScreen: false, verb: verb, objectName: objectName);
                }
            }

            Document.Editor.UpdateScreen();
        }

        /// <summary>
        /// New check - start the settings dialog and check.
        /// </summary>
        /// <returns></returns>
        public bool NewCheckWithDialog()
        {
            var dialog = DialogService.Instance.CreateDialog<ActionsSettingDialog>(null, _actionsSettingViewModel);
            var dialogResult = dialog.ShowDialog();
            if (dialogResult == null || !dialogResult.Value)
                return false;

            // Clear all check results first.
            ClearCheckResults(raiseEvent: true);

            var selectedIds = GetSelectedObjectIds();
            if (selectedIds == null || !selectedIds.Any())
            {
                DialogService.Instance.ShowMessageBox(_actionsSettingViewModel, "图形区没有实体可检查", "提示：",
                    MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
                return false;
            }

            // If breaking objects, we need to break all crosing objects first.
            if (_actionsSettingViewModel.ActionSelectVM.BreakCrossingObjects)
            {
                using (var toleranceSWitcher = new SafeToleranceOverride())
                {
                    var algorithm = new BreakCrossingObjectsBsp(Document.Editor, 0.0);
                    algorithm.Check(selectedIds);

                    Document.Editor.WriteMessage("\n共找到{0}个交叉对象，开始打断...", algorithm.CrossingInfos.Count());
                    algorithm.Fix(eraseOld:true);
                    Document.Editor.WriteMessage("\n打断所有交叉对象成功!\n");
                }
            }

            // Create actions
            _executingActions.Clear();
            foreach (var action in _actionsSettingViewModel.ActionSelectVM.CheckedItems)
            {
                _executingActions.Add(action.ActionType);
            }

            // After objects are broken, need to update selectedIds.
            if (_actionsSettingViewModel.ActionSelectVM.BreakCrossingObjects)
            {
                selectedIds = GetSelectedObjectIds();
            }

            NewCheckWithPrompt(selectedIds, "拓扑错误");
            return true;
        }

        private void NewCheckWithPrompt(IEnumerable<ObjectId> selectedIds, string objectName)
        {
            Document.Editor.WriteMessage("\n开始检查{0}...", objectName);
            int count = 0;
            var agents = _actionAgents.Where(it => _executingActions.Contains(it.Key)).Select(it => it.Value);
            foreach (var agent in agents)
            {
                var checkResults = agent.Action.Check(selectedIds);
                count += checkResults.Count();
                var group = new CheckResultGroup(agent.ActionType, checkResults);
                AddCheckResultGroup(group, raiseEvent: true);
            }
            Document.Editor.WriteMessage("\n检查到{0}处{1}！\n", count, objectName);
        }

        private void Recheck(HashSet<ObjectId> rejectedObjIds, string objectName)
        {
            Document.Editor.WriteMessage("\n重新检查{0}...", objectName);

            // Clear all check results
            if(PanelViewModel != null)
                PanelViewModel.CheckResultsVM.Clear();
            ClearCheckResults(raiseEvent: false);

            // Recheck
            IEnumerable<ObjectId> selectedIds = null;
            if (GetCheckObjectIds != null)
                selectedIds = GetCheckObjectIds(Document);
            else
                selectedIds = GetAllPolylineObjectIds();

            if (selectedIds == null || !selectedIds.Any())
                return;

            int count = 0;
            var agents = _actionAgents.Where(it => _executingActions.Contains(it.Key)).Select(it => it.Value);
            foreach (var agent in agents)
            {
                var checkResults = agent.Action.Check(selectedIds);
                count += checkResults.Count();
                if (rejectedObjIds != null)
                {
                    foreach (var checkResult in checkResults)
                    {
                        var ids = checkResult.SourceIds.Intersect(rejectedObjIds);
                        if (ids.Any())
                            checkResult.Status = Status.Rejected;
                    }
                }
                var group = new CheckResultGroup(agent.ActionType, checkResults);
                AddCheckResultGroup(group, raiseEvent: true);
            }

            if (PostHandleAfterCheck != null)
                PostHandleAfterCheck(Document, selectedIds);
            Document.Editor.WriteMessage("\n检查到{0}处{1}", count, objectName);
        }

        private void FixAllCheckResultsWithProgresser(bool updateScreen, string verb, string objectName)
        {
            var checkResults = new List<CheckResult>();
            foreach (var checkResultGroup in CheckResultGroups)
            {
                checkResults.AddRange(checkResultGroup.Value.CheckResults);
            }

            var promptText = String.Format("正在{0}{1}中...", verb, objectName);
            using (var progresser = new SimpleLongOperationManager(promptText))
            {
                progresser.SetTotalOperations(checkResults.Count);
                foreach (var checkresult in checkResults)
                {
                    FixCheckResult(checkresult, updateScreen: false);
                    progresser.Tick();
                }
            }

            // Udpate screen
            if (updateScreen)
                Document.Editor.UpdateScreen();
        }

        private HashSet<ObjectId> GetAllRejectedObjectIds()
        {
            // Record rejected ObjectIds.
            var rejectedObjIds = new HashSet<ObjectId>();
            foreach (var checkResultGroup in _checkResultGroups)
            {
                foreach (var checkResult in checkResultGroup.Value.CheckResults)
                {
                    if (checkResult.Status != Status.Rejected)
                        continue;
                    foreach (var sourcdId in checkResult.SourceIds)
                    {
                        rejectedObjIds.Add(sourcdId);
                    }
                }
            }
            return rejectedObjIds;
        }

        private bool NeedContinueFixAllCheckResults()
        {
            foreach (var checkResultGroup in _checkResultGroups)
            {
                foreach (var checkResult in checkResultGroup.Value.CheckResults)
                {
                    if (checkResult.Status == Status.Pending || checkResult.Status == Status.Invalid)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private IEnumerable<ObjectId> GetAllPolylineObjectIds()
        {
            var objectIds = new List<ObjectId>();
            var selectionRes = Document.Editor.SelectImplied();
            if (selectionRes.Status != PromptStatus.Error && selectionRes.Value.Count >= 1)
            {
                var selectedObjects = selectionRes.Value.GetObjectIds();

                // 在选择集中挑选是Polyline和Polyline2d的对象
                using (var tr = Document.Database.TransactionManager.StartTransaction())
                {
                    foreach (var selectedObject in selectedObjects)
                    {
                        var entity = tr.GetObject(selectedObject, OpenMode.ForRead);
                        if (!IsVisiblePolyline(entity, tr))
                            continue;

                        objectIds.Add(selectedObject);
                    }

                    tr.Commit();
                    return objectIds;
                }
            }

            // 否则直接返回所有的多边形对象
            return GetAllVisiblePolylineObjectIds(Document);
        }

        private IEnumerable<ObjectId> GetAllVisiblePolylineObjectIds(Document document)
        {
            var objectIds = new List<ObjectId>();
            using (var transaction = document.Database.TransactionManager.StartTransaction())
            {
                var modelspaceId = SymbolUtilityServices.GetBlockModelSpaceId(document.Database);
                var modelspace = (BlockTableRecord) transaction.GetObject(modelspaceId, OpenMode.ForRead);
                foreach (ObjectId objId in modelspace)
                {
                    var entity = transaction.GetObject(objId, OpenMode.ForRead);
                    if (!IsVisiblePolyline(entity, transaction))
                        continue;

                    objectIds.Add(objId);
                }
                transaction.Commit();
            }
            return objectIds;
        }

        private bool IsVisiblePolyline(DBObject entity, Transaction transaction)
        {
            var type = entity.GetType();
            if (type != typeof(Polyline) && type != typeof(Polyline2d))
                return false;

            var layer = (LayerTableRecord)transaction.GetObject(((Entity)entity).LayerId, OpenMode.ForRead);
            if (layer.IsOff)
                return false;

            return true;
        }

        private IEnumerable<ObjectId> GetSelectedObjectIds()
        {
            var selectedIds = new List<ObjectId>();
            if (_actionsSettingViewModel == null)
            {
                // Get all entity ids of database.
                var allIds = LayerUtils.GetObjectIdcollectionFromLayerNames(Document.Database, "*");
                return allIds;
            }

            if (_actionsSettingViewModel.EntitySelectVM.IsSelectAll)
            {
                var allSelectedIds = LayerUtils.GetObjectIdcollectionFromLayerNames(Document.Database,
                    _actionsSettingViewModel.EntitySelectVM.LayersText).ToList();

                var fixedIds = _actionsSettingViewModel.EntitySelectVM.FixedObjectIds;

                // Remove the fixed items from all entities.
                foreach (ObjectId fixedId in fixedIds)
                {
                    if (allSelectedIds.Contains(fixedId))
                        allSelectedIds.Remove(fixedId);
                }

                return allSelectedIds;
            }
            else
                return _actionsSettingViewModel.EntitySelectVM.SelectedObjectIds;
        }

        #endregion

        #region Check Results && Actions
        protected void AddCheckResultGroup(CheckResultGroup group, bool raiseEvent)
        {
            if (group == null)
                return;

            _checkResultGroups[group.ActionType] = group;
            if (raiseEvent && CheckResultGroupsAdded != null)
            {
                CheckResultGroupsAdded(this, new CheckResultGroupEventArgs()
                {
                    CheckResultGroups = new CheckResultGroup[] { group}
                });
            }

            // Index each check result
            foreach (var checkResult in group.CheckResults)
            {
                foreach (var objId in checkResult.SourceIds)
                {
                    if (_checkResultIndices.ContainsKey(objId))
                    {
                        _checkResultIndices[objId].Add(checkResult);
                    }
                    else
                    {
                        _checkResultIndices[objId] = new List<CheckResult>(new CheckResult[] {checkResult});
                    }
                }
            }
        }

        protected void RemoveCheckResultGroup(ActionType actionType, bool raiseEvent)
        {
            if (!_checkResultGroups.ContainsKey(actionType))
                return;

            var group = _checkResultGroups[actionType];
            _checkResultGroups.Remove(actionType);
            if (raiseEvent && CheckResultGroupsRemoved != null)
            {
                CheckResultGroupsRemoved(this, new CheckResultGroupEventArgs()
                {
                    CheckResultGroups = new CheckResultGroup[] { group}
                });
            }

            // Dispose the group
            group.Dispose();
        }

        private void ClearCheckResults(bool raiseEvent)
        {
            _checkResultIndices.Clear();
            var groups = _checkResultGroups.Values.ToArray();
            _checkResultGroups.Clear();
            if (raiseEvent && CheckResultGroupsRemoved != null)
            {
                CheckResultGroupsRemoved(this, new CheckResultGroupEventArgs()
                {
                    CheckResultGroups = groups 
                });
            }
            foreach (var checkResultGroup in groups)
            {
                checkResultGroup.Dispose();
            }
        }

        private void ShowTransientErrorMark(CheckResult checkResult)
        {
            var markshape = ErrorMarkSettings.CurrentSettings.MarkShapes[checkResult.ActionType];
            var color = ErrorMarkSettings.CurrentSettings.MarkColors[checkResult.ActionType];
            var acadColor = AcadColor.FromRgb(color.R, color.G, color.B);
            _transientGraphicsMgr.CreateTransientErrorMarks(markshape, checkResult.MarkPoints, acadColor, checkResult.TransientDrawables);
        }

        private void HighlightCheckResultEntities(CheckResult result, bool highlight, Transaction transaction)
        {
            IEnumerable<ObjectId> ids = null;
            if (result.Status == Status.Fixed)
                ids = result.TargetIds;
            else
                ids = result.SourceIds;
            foreach (var id in ids)
            {
                if (!id.IsValid || id.IsErased)
                    continue;
                var entity = (Entity) transaction.GetObject(id, OpenMode.ForRead);
                if (highlight)
                    entity.Highlight();
                else
                    entity.Unhighlight();
                entity.Dispose();
            }
        }
        #endregion

        #region PaletteSet Visible State
        private bool? _panelPreviousVisible = null;

        private bool _recordPanelPreviousState = true;
        public bool RecordPanelPreviousState 
        { 
            get { return _recordPanelPreviousState; }
            set { _recordPanelPreviousState = value; }
        }

        public void ShowMapCleanPanel(bool show, bool recordState)
        {
            using (var switcher = new RecordPanelPreviousStateSwitcher(this, recordState))
            {
                if (show)
                    AllPaletteSets.DisplayPaletteSet(PaletteSetType.MapClean, Document);
                else
                    AllPaletteSets.ClosePaletteSet(PaletteSetType.MapClean);
            }
        }

        private void OnPanelVisibilityChanged(object sender, PaletteVisibleEventArgs e)
        {
            if (RecordPanelPreviousState)
                _panelPreviousVisible = e.IsVisible;
        }

        class RecordPanelPreviousStateSwitcher : IDisposable
        {
            private MapCleanService _service;
            private bool _oldValue;

            public RecordPanelPreviousStateSwitcher(MapCleanService service, bool newVal)
            {
                _service = service;
                _oldValue = service.RecordPanelPreviousState;
                _service.RecordPanelPreviousState = newVal;
            }

            public void Dispose()
            {
                _service.RecordPanelPreviousState = _oldValue;
            }
        }
        #endregion

        #region Action sequence paletteset
        private bool? _actionPalettePreviousVisible = null;

        private bool _recordActionPalettePreviousState = true;
        public bool RecordActionPalettePreviousState
        {
            get { return _recordActionPalettePreviousState; }
            set { _recordActionPalettePreviousState = value; }
        }

        public void ShowActionPalette(bool show, bool recordState)
        {
            using (var switcher = new RecordActionPalettePreviousStateSwitcher(this, recordState))
            {
                if (show)
                    AllPaletteSets.DisplayPaletteSet(PaletteSetType.ActionSequence, Document);
                else
                    AllPaletteSets.ClosePaletteSet(PaletteSetType.ActionSequence);
            }
        }

        private void OnActionPaletteVisibilityChanged(object sender, PaletteVisibleEventArgs e)
        {
            if (RecordActionPalettePreviousState)
                _actionPalettePreviousVisible = e.IsVisible;
        }

        class RecordActionPalettePreviousStateSwitcher : IDisposable
        {
            private MapCleanService _service;
            private bool _oldValue;

            public RecordActionPalettePreviousStateSwitcher(MapCleanService service, bool newVal)
            {
                _service = service;
                _oldValue = service.RecordActionPalettePreviousState;
                _service.RecordActionPalettePreviousState = newVal;
            }

            public void Dispose()
            {
                _service.RecordActionPalettePreviousState = _oldValue;
            }
        }
        #endregion

        #region Events
        public event EventHandler<CheckResultGroupEventArgs> CheckResultGroupsAdded;
        public event EventHandler<CheckResultGroupEventArgs> CheckResultGroupsRemoved;
        #endregion

        #region Event Handlers
        private void OnDocumentToBeActivated(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document != Document)
                _transientGraphicsMgr.ClearTransientGraphics();
        }

        private void OnDocumentToBeDeactivated(object sender, DocumentCollectionEventArgs e)
        {
            if(e.Document == Document)
                _transientGraphicsMgr.ClearTransientGraphics();
        }

        private void OnDocumentBecameCurrent(object sender, DocumentCollectionEventArgs e)
        {
            // If the current document is not equal to this.Document, close the map clean panel.
            if (e.Document == Document)
            {
                if (_panelPreviousVisible != null && _panelPreviousVisible.Value)
                    ShowMapCleanPanel(show: true, recordState: false);

                if (_actionPalettePreviousVisible != null && _actionPalettePreviousVisible.Value)
                    ShowActionPalette(show: true, recordState: false);
            }
            else
            {
                // If the current document is equal to this.Document, show the map clean panel.
                if (AllPaletteSets.IsPaletteSetVisible(PaletteSetType.MapClean))
                {
                    _panelPreviousVisible = true;
                    ShowMapCleanPanel(show: false, recordState: false);
                }

                if (AllPaletteSets.IsPaletteSetVisible(PaletteSetType.ActionSequence))
                {
                    _actionPalettePreviousVisible = true;
                    ShowActionPalette(show: false, recordState: false);
                }
            }
        }

        private void OnDocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            // If the document is equal to this.Document, hide the map clean panel.
            if (e.Document == Document)
            {
                this.End();
            }
        }

        private void OnCommandWillStart(object sender, CommandEventArgs e)
        {
            _transientGraphicsMgr.ClearTransientGraphics();
        }

        private void OnActionStatusChanged(object sender, EventArgs e)
        {
            var actionAgent = sender as ActionAgent;
            if (actionAgent == null)
                return;

            foreach (var pair in _actionAgents)
            {
                if (pair.Key == actionAgent.ActionType)
                    continue;

                var agent = pair.Value;
                if (!agent.Dependencies.Contains(actionAgent.ActionType))
                    continue;

                bool enable = true;
                foreach (var actionType in agent.Dependencies)
                {
                    var dependencyAgent = _actionAgents[actionType];
                    if ((dependencyAgent.Status & ActionStatus.Executed) != ActionStatus.Executed)
                    {
                        enable = false;
                        break;
                    }
                }

                if (enable)
                {
                    agent.Status |= ActionStatus.Pending;
                }
                else
                {
                    agent.Status &= (~ActionStatus.Pending);
                }
            }
        }

        #endregion
    }
}
