using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace GodofDrakes.CommandPalette.OSRS.ViewModel;

public sealed partial class WikiPageViewModel : ReactiveObject
{
	[Reactive]
	public required string Title { get; set; }

	[Reactive]
	public string? Description { get; set; }

	[Reactive]
	public string? Url { get; set; }

	[Reactive]
	public string? ThumbnailUrl { get; set; }

	[Reactive]
	public bool IsRedirector { get; set; }
}