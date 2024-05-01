using Fluxor;
using Microsoft.JSInterop;
using Luthetus.Common.RazorLib.Storages.States;
using Luthetus.Common.RazorLib.Storages.Models;
using Luthetus.Common.RazorLib.Themes.States;
using Luthetus.Common.RazorLib.Themes.Models;
using Luthetus.Common.RazorLib.BackgroundTasks.Models;
using Luthetus.Common.RazorLib.Keys.Models;
using Luthetus.TextEditor.RazorLib.Diffs.States;
using Luthetus.TextEditor.RazorLib.FindAlls.States;
using Luthetus.TextEditor.RazorLib.Groups.States;
using Luthetus.TextEditor.RazorLib.Options.States;
using Luthetus.TextEditor.RazorLib.TextEditors.States;
using Luthetus.TextEditor.RazorLib.Installations.Models;
using Luthetus.TextEditor.RazorLib.TextEditors.Models.TextEditorServices;
using Luthetus.TextEditor.RazorLib.Decorations.Models;
using static Luthetus.TextEditor.RazorLib.TextEditors.Models.TextEditorServices.ITextEditorService;

using System.Collections.Immutable;
using Luthetus.TextEditor.RazorLib.Edits.States;

namespace Luthetus.TextEditor.RazorLib.TextEditors.Models;

public partial class TextEditorService : ITextEditorService
{
    /// <summary>
    /// See explanation of this field at: <see cref="TextEditorAuthenticatedAction"/>
    /// </summary>
    public static readonly Key<TextEditorAuthenticatedAction> AuthenticatedActionKey = new(Guid.Parse("13831968-9b10-46d1-8d47-842b78238d6a"));

    private readonly IBackgroundTaskService _backgroundTaskService;
    private readonly IDispatcher _dispatcher;
    private readonly LuthetusTextEditorConfig _textEditorOptions;
    private readonly ITextEditorRegistryWrap _textEditorRegistryWrap;
    private readonly IStorageService _storageService;
    // TODO: Perhaps do not reference IJSRuntime but instead wrap it in a 'IUiProvider' or something like that. The 'IUiProvider' would then expose methods that allow the TextEditorViewModel to adjust the scrollbars. 
    private readonly IJSRuntime _jsRuntime;
    private readonly StorageSync _storageSync;

    public TextEditorService(
        IState<TextEditorModelState> modelStateWrap,
        IState<TextEditorViewModelState> viewModelStateWrap,
        IState<TextEditorGroupState> groupStateWrap,
        IState<TextEditorDiffState> diffStateWrap,
        IState<ThemeState> themeStateWrap,
        IState<TextEditorOptionsState> optionsStateWrap,
        IState<TextEditorFindAllState> findAllStateWrap,
        IBackgroundTaskService backgroundTaskService,
        LuthetusTextEditorConfig textEditorOptions,
        ITextEditorRegistryWrap textEditorRegistryWrap,
        IStorageService storageService,
        IJSRuntime jsRuntime,
        StorageSync storageSync,
        IDispatcher dispatcher)
    {
        ModelStateWrap = modelStateWrap;
        ViewModelStateWrap = viewModelStateWrap;
        GroupStateWrap = groupStateWrap;
        DiffStateWrap = diffStateWrap;
        ThemeStateWrap = themeStateWrap;
        OptionsStateWrap = optionsStateWrap;
        FindAllStateWrap = findAllStateWrap;

        _backgroundTaskService = backgroundTaskService;
        _textEditorOptions = textEditorOptions;
        _textEditorRegistryWrap = textEditorRegistryWrap;
        _storageService = storageService;
        _jsRuntime = jsRuntime;
        _storageSync = storageSync;
        _dispatcher = dispatcher;

        ModelApi = new TextEditorModelApi(this, _textEditorRegistryWrap.DecorationMapperRegistry, _textEditorRegistryWrap.CompilerServiceRegistry, _backgroundTaskService, _dispatcher);
        ViewModelApi = new TextEditorViewModelApi(this, _backgroundTaskService, ViewModelStateWrap, ModelStateWrap, _jsRuntime, _dispatcher);
        GroupApi = new TextEditorGroupApi(this, _dispatcher);
        DiffApi = new TextEditorDiffApi(this, _dispatcher);
        OptionsApi = new TextEditorOptionsApi(this, _textEditorOptions, _storageService, _storageSync, _dispatcher);
        FindAllApi = new TextEditorFindAllApi(this, _dispatcher);
    }

    public IState<TextEditorModelState> ModelStateWrap { get; }
    public IState<TextEditorViewModelState> ViewModelStateWrap { get; }
    public IState<TextEditorGroupState> GroupStateWrap { get; }
    public IState<TextEditorDiffState> DiffStateWrap { get; }
    public IState<ThemeState> ThemeStateWrap { get; }
    public IState<TextEditorOptionsState> OptionsStateWrap { get; }
    public IState<TextEditorFindAllState> FindAllStateWrap { get; }

    
#if DEBUG
    public string StorageKey => "luth_te_text-editor-options-debug";
#else
    public string StorageKey => "luth_te_text-editor-options";
#endif

    public string ThemeCssClassString => ThemeStateWrap.Value.ThemeList.FirstOrDefault(
        x => x.Key == OptionsStateWrap.Value.Options.CommonOptions.ThemeKey)
        ?.CssClassString
            ?? ThemeFacts.VisualStudioDarkThemeClone.CssClassString;

    public ITextEditorModelApi ModelApi { get; }
    public ITextEditorViewModelApi ViewModelApi { get; }
    public ITextEditorGroupApi GroupApi { get; }
    public ITextEditorDiffApi DiffApi { get; }
    public ITextEditorOptionsApi OptionsApi { get; }
    public ITextEditorFindAllApi FindAllApi { get; }

    /// Goal: Change BackgroundTask to act similarly to IThrottleEvent #Step 600 (2024-03-11)
    /// -------------------------------------------------------------------------------------
    /// The TextEditorService's 'Post(...)' method needs to be changed.
    /// It needs to accept an 'ITextEditorTask', where 'ITextEditorTask'
    /// inherits 'IBackgroundTask'
    ///
    /// The reason for this is due to the 'edit context' which all 'TextEditorEdit'
    /// currently receive.
    ///
    /// The 'editContext' provides mutable state for the text editor,
    /// then any mutated state is written out once the 'Post(...)' method is finished.
    ///
    /// So, in order to provide the 'editContext' to 'IBackgroundTask', some other
    /// type is needed. I'm unsure if it will be an interface, class, abstract or etc...
    public void Post(ITextEditorTask innerTask)
	{
		var editContext = new TextEditorEditContext(
	        this,
	        AuthenticatedActionKey);

		var textEditorServiceTask = new TextEditorServiceTask(
			innerTask,
			editContext,
			_dispatcher);

		_backgroundTaskService.Enqueue(textEditorServiceTask);
	}

    public void Post(string taskDisplayName, TextEditorEdit edit)
    {
        _backgroundTaskService.Enqueue(Key<BackgroundTask>.NewKey(),
            ContinuousBackgroundTaskWorker.GetQueueKey(),
            "te_" + taskDisplayName,
                async () =>
                {
                    var editContext = new TextEditorEditContext(
                        this,
                        AuthenticatedActionKey);

                    await edit.Invoke(editContext).ConfigureAwait(false);

                    foreach (var modelModifier in editContext.ModelCache.Values)
                    {
                        if (modelModifier is null || !modelModifier.WasModified)
                            continue;

                        _dispatcher.Dispatch(new TextEditorModelState.SetAction(
                            AuthenticatedActionKey,
                            editContext,
                            modelModifier));

                        var viewModelBag = ModelApi.GetViewModelsOrEmpty(modelModifier.ResourceUri);

                        foreach (var viewModel in viewModelBag)
                        {
                            // Invoking 'GetViewModelModifier' marks the view model to be updated.
                            editContext.GetViewModelModifier(viewModel.ViewModelKey);
                        }

                        if (modelModifier.WasDirty != modelModifier.IsDirty)
                        {
                            if (modelModifier.IsDirty)
                                _dispatcher.Dispatch(new DirtyResourceUriState.AddDirtyResourceUriAction(modelModifier.ResourceUri));
                            else
                                _dispatcher.Dispatch(new DirtyResourceUriState.RemoveDirtyResourceUriAction(modelModifier.ResourceUri));
                        }
                    }

                    foreach (var viewModelModifier in editContext.ViewModelCache.Values)
                    {
                        if (viewModelModifier is null || !viewModelModifier.WasModified)
                            return;

                        var successCursorModifierBag = editContext.CursorModifierBagCache.TryGetValue(
                            viewModelModifier.ViewModel.ViewModelKey,
                            out var cursorModifierBag);

                        if (successCursorModifierBag && cursorModifierBag is not null)
                        {
                            viewModelModifier.ViewModel = viewModelModifier.ViewModel with
                            {
                                CursorList = cursorModifierBag.List
                                    .Select(x => x.ToCursor())
                                    .ToImmutableArray()
                            };
                        }

                        await ViewModelApi.CalculateVirtualizationResultFactory(
                                viewModelModifier.ViewModel.ResourceUri, viewModelModifier.ViewModel.ViewModelKey, CancellationToken.None)
                            .Invoke(editContext)
							.ConfigureAwait(false);

                        _dispatcher.Dispatch(new TextEditorViewModelState.SetViewModelWithAction(
                            AuthenticatedActionKey,
                            editContext,
                            viewModelModifier.ViewModel.ViewModelKey,
                            inState => viewModelModifier.ViewModel));
                    }
                });
    }
}