using Luthetus.Common.RazorLib.Keys.Models;
using Luthetus.TextEditor.RazorLib.CompilerServices.GenericLexer.Decoration;
using Luthetus.TextEditor.RazorLib.Lexes.Models;
using Luthetus.TextEditor.RazorLib.TextEditors.Models.Internals;
using Luthetus.TextEditor.RazorLib.TextEditors.Models.TextEditorModels;
using Luthetus.TextEditor.RazorLib.TextEditors.Models.TextEditorServices;
using Luthetus.TextEditor.RazorLib.TextEditors.Models;
using Microsoft.AspNetCore.Components;
using Luthetus.CompilerServices.Lang.JavaScript;

namespace TestServer.Components.Pages;

public partial class Home
{
    [Inject]
    private ITextEditorService TextEditorService { get; set; } = null!;
    TextEditorViewModelDisplayOptions DisplayOptions { get; set; } = new();
    private static readonly ResourceUri TextEditorResourceUri = new ResourceUri("/");
    private static readonly Key<TextEditorViewModel> TextEditorViewModelKey = Key<TextEditorViewModel>.NewKey();
    protected override void OnInitialized()
    {
        var textEditorModel = new TextEditorModel(
            TextEditorResourceUri,
            DateTime.UtcNow,
            ExtensionNoPeriodFacts.JAVA_SCRIPT,
            string.Empty,
            new GenericDecorationMapper(),
            new JavaScriptCompilerService(TextEditorService));
        TextEditorService.ModelApi.RegisterCustom(textEditorModel);
        textEditorModel.CompilerService.RegisterResource(textEditorModel.ResourceUri);

        TextEditorService.ViewModelApi.Register(
            TextEditorViewModelKey,
            TextEditorResourceUri,
            new TextEditorCategory(nameof(Home)));

        base.OnInitialized();
    }
}
