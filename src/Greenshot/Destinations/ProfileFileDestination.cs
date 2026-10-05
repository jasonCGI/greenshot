/* Greenshot, licensed under the GNU General Public License, version 1 or later. */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapplo.Ini;
using Greenshot.Base.Core;
using Greenshot.Base.Core.FileFormat;
using Greenshot.Base.Interfaces;
using Greenshot.Configuration;

namespace Greenshot.Destinations
{
    /// <summary>A profile used for one save, without changing output defaults.</summary>
    public sealed class ProfileFileDestination : DestinationBase
    {
        public ExportProfile Profile { get; }
        public ProfileFileDestination(ExportProfile profile) { Profile = profile; }
        public override string Designation => "SaveProfile:" + Profile.Name;
        public override DestinationDescriptor Descriptor => new DestinationDescriptor(
            "Save as " + Profile.Name, 0, DestinationIcons.Resource("Save.Image"));

        public static IEnumerable<ProfileFileDestination> GetChoices(ICoreConfiguration config)
        {
            // Fixed policies take priority over temporary profile overrides too.
            string[] properties = { nameof(config.OutputFileFormat), nameof(config.OutputFileDpiPreset),
                nameof(config.OutputFileCustomDpi), nameof(config.OutputFileJpegQuality), nameof(config.OutputFileWebpLossless),
                nameof(config.OutputFileWebpQuality), nameof(config.OutputFileReduceColors),
                nameof(config.OutputFileAutoReduceColors), nameof(config.OutputFilePromptQuality) };
            if (properties.Any(config.IsConstant)) return Array.Empty<ProfileFileDestination>();
            var profiles = ExportProfile.Defaults();
            try { profiles.AddRange(ExportProfile.Deserialize(config.OutputExportProfiles)); }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is FormatException)
            {
                // Keep damaged storage intact. Built-in choices remain available.
            }
            var registry = SimpleServiceProvider.Current.GetInstance<IFileFormatRegistry>(true);
            return profiles.Where(p => registry != null && registry.TryGet(p.Format, out _))
                .Select(p => new ProfileFileDestination(p)).ToList();
        }

        public override async Task<ExportResult> ExportAsync(ExportRequest request, CancellationToken cancellationToken)
        {
            // Recheck policy when clicked, as configuration may have changed while the picker was open.
            if (!GetChoices(CoreConfiguration).Any(p => p.Profile.Name == Profile.Name)) return ExportResult.Declined;
            var savedTo = await FileDestination.SaveWithDialogAsync(request,
                CoreConfiguration.OutputFileCopyPathToClipboard, cancellationToken, Profile).ConfigureAwait(false);
            return savedTo == null ? ExportResult.Declined : FileDestination.Saved(request.Metadata, savedTo);
        }
    }
}
