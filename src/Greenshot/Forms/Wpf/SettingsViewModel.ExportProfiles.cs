/*
 * Greenshot - a free and open source screenshot tool
 * Copyright (C) 2007-2026 Thomas Braun, Jens Klingen, Robin Krom
 * Licensed under the GNU General Public License, version 1 or later.
 */
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Dapplo.Ini;
using Greenshot.Base;
using Greenshot.Base.Core;
using Greenshot.Base.Core.FileFormat;
using Greenshot.Base.Interfaces;

namespace Greenshot.Forms.Wpf
{
    public partial class SettingsViewModel
    {
        public ObservableCollection<ExportProfile> ExportProfiles { get; } = new ObservableCollection<ExportProfile>();
        public ExportProfile SelectedExportProfile { get; set; }
        public string ExportProfileName { get; set; }
        private string _exportProfileStatus;
        private bool _profileStorageValid = true;
        public string ExportProfileStatus
        {
            get => _exportProfileStatus;
            private set { _exportProfileStatus = value; OnPropertyChanged(); }
        }

        private void InitializeExportProfiles()
        {
            foreach (var profile in ExportProfile.Defaults()) ExportProfiles.Add(profile);
            SelectedExportProfile = ExportProfiles.First();
            try { foreach (var profile in ExportProfile.Deserialize(CoreConfiguration.OutputExportProfiles)) ExportProfiles.Add(profile); }
            catch (Exception exception) when (exception is ArgumentException || exception is FormatException || exception is InvalidOperationException || exception is System.Xml.XmlException)
            {
                _profileStorageValid = false;
                ExportProfileStatus = "Saved profiles could not be loaded. Existing profile storage has been kept.";
            }
        }

        public void ApplyExportProfile()
        {
            RunProfileAction(() =>
            {
                if (SelectedExportProfile == null) throw new ArgumentException("Select a profile to apply.");
                var registry = SimpleServiceProvider.Current.GetInstance<IFileFormatRegistry>(true);
                if (registry == null || !registry.GetSaveableFileFormats().Any(f => string.Equals(f.Id, SelectedExportProfile.Format, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException("This profile's image format is unavailable.");
                SelectedExportProfile.Apply(CoreConfiguration);
                ExportProfileStatus = "Applied " + SelectedExportProfile.Name + ". Pixel dimensions are unchanged.";
            });
        }

        public void SaveExportProfile()
        {
            RunProfileAction(() =>
            {
                EnsureProfileStorageWritable();
                var profile = ExportProfile.Capture(ExportProfileName, CoreConfiguration);
                if (ExportProfiles.Any(p => string.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException("That name already exists. Use a new name or delete the custom profile first.");
                var saved = ExportProfiles.Where(p => !p.BuiltIn).ToList();
                saved.Add(profile);
                CoreConfiguration.OutputExportProfiles = ExportProfile.Serialize(saved);
                ExportProfiles.Add(profile);
                ExportProfileStatus = "Saved " + profile.Name + ".";
            });
        }

        public void DeleteExportProfile()
        {
            RunProfileAction(() =>
            {
                EnsureProfileStorageWritable();
                if (SelectedExportProfile == null || SelectedExportProfile.BuiltIn)
                    throw new ArgumentException("Select a custom profile to delete. Built-in profiles are kept.");
                var removed = SelectedExportProfile;
                CoreConfiguration.OutputExportProfiles = ExportProfile.Serialize(ExportProfiles.Where(p => !p.BuiltIn && p != removed).ToList());
                ExportProfiles.Remove(removed);
                SelectedExportProfile = null;
                OnPropertyChanged(nameof(SelectedExportProfile));
                ExportProfileStatus = "Deleted " + removed.Name + ".";
            });
        }

        private void EnsureProfileStorageWritable()
        {
            if (!_profileStorageValid) throw new InvalidOperationException("Repair the saved profile configuration before saving or deleting profiles.");
            if (CoreConfiguration.IsConstant(nameof(CoreConfiguration.OutputExportProfiles)))
                throw new InvalidOperationException("Export profile storage is fixed by configuration policy.");
        }

        private void RunProfileAction(Action action)
        {
            try { action(); }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { ExportProfileStatus = exception.Message; }
        }
    }
}
