﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Bimangle.ForgeEngine.Common.Formats.Cesium3DTiles;
using Bimangle.ForgeEngine.Common.Formats.Cesium3DTiles.Revit;
using Bimangle.ForgeEngine.Common.Types;
using Bimangle.ForgeEngine.Revit.Config;
using Bimangle.ForgeEngine.Revit.Core;
using Bimangle.ForgeEngine.Revit.Helpers;
using Bimangle.ForgeEngine.Revit.Utility;
using TextBox = System.Windows.Forms.TextBox;
#if EXPRESS
using ExporterX = Bimangle.ForgeEngine.Revit.Express.Cesium3DTiles.Exporter;
#else
using ExporterX = Bimangle.ForgeEngine.Revit.Pro.Cesium3DTiles.Exporter;
#endif

namespace Bimangle.ForgeEngine.Revit.UI.Controls
{
    [Browsable(false)]
    partial class ExportCesium3DTiles : UserControl, IExportControl
    {
        private UIDocument _UIDocument;
        private View3D _View;
        private AppConfig _Config;
        private AppConfigCesium3DTiles _LocalConfig;
        private Dictionary<int, bool> _ElementIds;
        private List<FeatureInfo> _Features;

        private List<VisualStyleInfo> _VisualStyles;
        private VisualStyleInfo _VisualStyleDefault;

        private List<ComboItemInfo> _LevelOfDetails;
        private ComboItemInfo _LevelOfDetailDefault;

        public TimeSpan ExportDuration { get; private set; }


        public ExportCesium3DTiles()
        {
            InitializeComponent();
        }

        string IExportControl.Title => InnerCommandExportCesium3DTiles.TITLE;

        string IExportControl.Icon => @"3dtiles";

        void IExportControl.Init(UIDocument uidoc, View3D view, AppConfig config, Dictionary<int, bool> elementIds)
        {
            _UIDocument = uidoc;
            _View = view;
            _Config = config;
            _LocalConfig = _Config.Cesium3DTiles;
            _ElementIds = elementIds;

            _Features = new List<FeatureInfo>
            {
                new FeatureInfo(FeatureType.ExcludeTexture, Strings.FeatureNameExcludeTexture, Strings.FeatureDescriptionExcludeTexture, true, false),
                new FeatureInfo(FeatureType.ExcludeLines, Strings.FeatureNameExcludeLines, Strings.FeatureDescriptionExcludeLines),
                new FeatureInfo(FeatureType.ExcludePoints, Strings.FeatureNameExcludePoints, Strings.FeatureDescriptionExcludePoints, true, false),
                new FeatureInfo(FeatureType.OnlySelected, Strings.FeatureNameOnlySelected, Strings.FeatureDescriptionOnlySelected),
                new FeatureInfo(FeatureType.ExportGrids, Strings.FeatureNameExportGrids, Strings.FeatureDescriptionExportGrids),
                new FeatureInfo(FeatureType.Wireframe, Strings.FeatureNameWireframe, Strings.FeatureDescriptionWireframe, true, false),
                new FeatureInfo(FeatureType.Gray, Strings.FeatureNameGray, Strings.FeatureDescriptionGray, true, false),
                new FeatureInfo(FeatureType.GenerateModelsDb, Strings.FeatureNameGenerateModelsDb, Strings.FeatureDescriptionGenerateModelsDb),
                new FeatureInfo(FeatureType.UseViewOverrideGraphic, Strings.FeatureNameUseViewOverrideGraphic, Strings.FeatureDescriptionUseViewOverrideGraphic, true, false),
                new FeatureInfo(FeatureType.UseBasicRenderColor, string.Empty, string.Empty, true, false),
                new FeatureInfo(FeatureType.UseGoogleDraco, Strings.FeatureNameUseGoogleDraco, Strings.FeatureDescriptionUseGoogleDraco, true, false),
                new FeatureInfo(FeatureType.ExtractShell, Strings.FeatureNameExtractShell, Strings.FeatureDescriptionExtractShell, true, false),
                new FeatureInfo(FeatureType.ExportSvfzip, Strings.FeatureNameExportSvfzip, Strings.FeatureDescriptionExportSvfzip, true, false),
                new FeatureInfo(FeatureType.EnableQuantizedAttributes, Strings.FeatureNameEnableQuantizedAttributes, Strings.FeatureDescriptionEnableQuantizedAttributes, true, false),
                new FeatureInfo(FeatureType.EnableTextureWebP, Strings.FeatureNameEnableTextureWebP, Strings.FeatureDescriptionEnableTextureWebP, true, false),
            };

            _VisualStyles = new List<VisualStyleInfo>();
            _VisualStyles.Add(new VisualStyleInfo(@"Wireframe", Strings.VisualStyleWireframe, new Dictionary<FeatureType, bool>
            {
                {FeatureType.ExcludeTexture, true},
                {FeatureType.Wireframe, true},
                {FeatureType.UseViewOverrideGraphic, false},
                {FeatureType.UseBasicRenderColor, false},
                {FeatureType.Gray, false}
            }));
            _VisualStyles.Add(new VisualStyleInfo(@"Gray", Strings.VisualStyleGray, new Dictionary<FeatureType, bool>
            {
                {FeatureType.ExcludeTexture, true},
                {FeatureType.Wireframe, false},
                {FeatureType.UseViewOverrideGraphic, false},
                {FeatureType.UseBasicRenderColor, false},
                {FeatureType.Gray, true}
            }));
            _VisualStyles.Add(new VisualStyleInfo(@"Colored", Strings.VisualStyleColored, new Dictionary<FeatureType, bool>
            {
                {FeatureType.ExcludeTexture, true},
                {FeatureType.Wireframe, false},
                {FeatureType.UseViewOverrideGraphic, true},
                {FeatureType.UseBasicRenderColor, false},
                {FeatureType.Gray, false}
            }));
            _VisualStyles.Add(new VisualStyleInfo(@"Textured", Strings.VisualStyleTextured + $@"({Strings.TextDefault})", new Dictionary<FeatureType, bool>
            {
                {FeatureType.ExcludeTexture, false},
                {FeatureType.Wireframe, false},
                {FeatureType.UseViewOverrideGraphic, false},
                {FeatureType.UseBasicRenderColor, true},
                {FeatureType.Gray, false}
            }));
            _VisualStyles.Add(new VisualStyleInfo(@"Realistic", Strings.VisualStyleRealistic, new Dictionary<FeatureType, bool>
            {
                {FeatureType.ExcludeTexture, false},
                {FeatureType.Wireframe, false},
                {FeatureType.UseViewOverrideGraphic, false},
                {FeatureType.UseBasicRenderColor, false},
                {FeatureType.Gray, false}
            }));
            _VisualStyleDefault = _VisualStyles.First(x => x.Key == @"Colored");

            const int DEFAULT_LEVEL_OF_DETAILS = 6;
            _LevelOfDetails = new List<ComboItemInfo>();
            _LevelOfDetails.Add(new ComboItemInfo(-1, Strings.TextAuto));
            for (var i = 0; i <= 15; i++)
            {
                string text;
                switch (i)
                {
                    case 0:
                        text = $@"{i} ({Strings.TextLowest})";
                        break;
                    case DEFAULT_LEVEL_OF_DETAILS:
                        text = $@"{i} ({Strings.TextNormal})";
                        break;
                    case 15:
                        text = $@"{i} ({Strings.TextHighest})";
                        break;
                    default:
                        text = i.ToString();
                        break;
                }

                _LevelOfDetails.Add(new ComboItemInfo(i, text));
            }
            _LevelOfDetailDefault = _LevelOfDetails.Find(x => x.Value == DEFAULT_LEVEL_OF_DETAILS);

            cbVisualStyle.Items.Clear();
            cbVisualStyle.Items.AddRange(_VisualStyles.Select(x => (object)x).ToArray());

            cbLevelOfDetail.Items.Clear();
            cbLevelOfDetail.Items.AddRange(_LevelOfDetails.Select(x => (object)x).ToArray());
        }

        bool IExportControl.Run()
        {
            var siteInfo = GetSiteInfo();
            if (siteInfo == null)
            {
                ShowMessageBox(Strings.SiteLocationInvalid);
                return false;
            }

            var filePath = txtTargetPath.Text;
            if (string.IsNullOrEmpty(filePath))
            {
                ShowMessageBox(Strings.MessageSelectOutputPathFirst);
                return false;
            }

#if !R2014
            if (CustomExporter.IsRenderingSupported() == false &&
                ShowConfigBox(Strings.ExportWillFailBecauseMaterialLib) == false)
            {
                return false;
            }
#endif

            if (File.Exists(filePath) &&
                ShowConfigBox(Strings.OutputFileExistedWarning) == false)
            {
                return false;
            }

            var visualStyle = cbVisualStyle.SelectedItem as VisualStyleInfo;
            if (visualStyle != null)
            {
                foreach (var p in visualStyle.Features)
                {
                    _Features.FirstOrDefault(x => x.Type == p.Key)?.ChangeSelected(_Features, p.Value);
                }
            }

            var levelOfDetail = (cbLevelOfDetail.SelectedItem as ComboItemInfo) ?? _LevelOfDetailDefault;

            #region 更新界面选项到 _Features

            void SetFeature(FeatureType featureType, bool selected)
            {
                _Features.FirstOrDefault(x => x.Type == featureType)?.ChangeSelected(_Features, selected);
            }

            //SetFeature(FeatureType.ExportGrids, cbIncludeGrids.Checked);

            SetFeature(FeatureType.ExcludeLines, cbExcludeLines.Checked);
            SetFeature(FeatureType.ExcludePoints, cbExcludeModelPoints.Checked);
            SetFeature(FeatureType.OnlySelected, cbExcludeUnselectedElements.Checked);

            SetFeature(FeatureType.UseGoogleDraco, cbUseDraco.Checked);
            SetFeature(FeatureType.ExtractShell, cbUseExtractShell.Checked);
            SetFeature(FeatureType.GenerateModelsDb, cbGeneratePropDbSqlite.Checked);
            SetFeature(FeatureType.ExportSvfzip, cbExportSvfzip.Checked);
            SetFeature(FeatureType.EnableQuantizedAttributes, cbEnableQuantizedAttributes.Checked);
            SetFeature(FeatureType.EnableTextureWebP, cbEnableTextureWebP.Checked);

            #endregion

            var isCancelled = false;
            using (var session = LicenseConfig.Create())
            {
                if (session.IsValid == false)
                {
                    LicenseConfig.ShowDialog(session, ParentForm);
                    return false;
                }

                #region 保存设置

                var config = _LocalConfig;
                config.Features = _Features.Where(x => x.Selected).Select(x => x.Type).ToList();
                config.LastTargetPath = txtTargetPath.Text;
                config.VisualStyle = visualStyle?.Key;
                config.LevelOfDetail = levelOfDetail?.Value ?? -1;
                config.Mode = rbModeBasic.Checked ? 0 : 1;
                _Config.Save();

                #endregion

                var sw = Stopwatch.StartNew();
                try
                {
                    var setting = new ExportSetting();
                    setting.LevelOfDetail = config.LevelOfDetail;
                    setting.OutputPath = config.LastTargetPath;
                    setting.Mode = config.Mode;
                    setting.Features = _Features.Where(x => x.Selected && x.Enabled).Select(x => x.Type).ToList();
                    setting.SelectedElementIds = _ElementIds?.Where(x => x.Value).Select(x => x.Key).ToList();
                    setting.Site = siteInfo;
                    setting.Oem = LicenseConfig.GetOemInfo(InnerApp.GetHomePath());

                    var hasSuccess = false;
                    using (var progress = new ProgressExHelper(this.ParentForm, Strings.MessageExporting))
                    {
                        var cancellationToken = progress.GetCancellationToken();

#if !DEBUG
                        //在有些 Revit 会遇到时不时无法转换的问题，循环多次重试, 应该可以成功
                        for (var i = 0; i < 5; i++)
                        {
                            try
                            {
                                StartExport(_UIDocument, _View, setting, progress.GetProgressCallback(), cancellationToken);
                                hasSuccess = true;
                                break;
                            }
                            catch (Autodesk.Revit.Exceptions.ExternalApplicationException)
                            {
                                Application.DoEvents();
                            }
                            catch (IOException ex)
                            {
                                ShowMessageBox("文件保存失败: " + ex.Message);
                                hasSuccess = true;
                                break;
                            }
                        }
#endif

                        //如果之前多次重试仍然没有成功, 这里再试一次，如果再失败就会给出稍后重试的提示
                        if (hasSuccess == false)
                        {
                            StartExport(_UIDocument, _View, setting, progress.GetProgressCallback(), cancellationToken);
                        }

                        isCancelled = cancellationToken.IsCancellationRequested;
                    }

                    sw.Stop();
                    var ts = sw.Elapsed;
                    ExportDuration = new TimeSpan(ts.Days, ts.Hours, ts.Minutes, ts.Seconds); //去掉毫秒部分

                    Debug.WriteLine(Strings.MessageOperationSuccessAndElapsedTime, ExportDuration);

                    if (isCancelled == false)
                    {
                        ShowMessageBox(string.Format(Strings.MessageExportSuccess, ExportDuration));
                    }
                }
                catch (IOException ex)
                {
                    sw.Stop();
                    Debug.WriteLine(Strings.MessageOperationFailureAndElapsedTime, sw.Elapsed);

                    ShowMessageBox(string.Format(Strings.MessageFileSaveFailure, ex.Message));
                }
                catch (Autodesk.Revit.Exceptions.ExternalApplicationException)
                {
                    sw.Stop();
                    Debug.WriteLine(Strings.MessageOperationFailureAndElapsedTime, sw.Elapsed);

                    ShowMessageBox(Strings.MessageOperationFailureAndTryLater);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    Debug.WriteLine(Strings.MessageOperationFailureAndElapsedTime, sw.Elapsed);

                    ShowMessageBox(ex.ToString());
                }
            }

            return isCancelled == false;
        }

        void IExportControl.Reset()
        {
            cbVisualStyle.SelectedItem = _VisualStyleDefault;
            cbLevelOfDetail.SelectedItem = _LevelOfDetailDefault;

            cbExcludeLines.Checked = true;
            cbExcludeModelPoints.Checked = true;
            cbExcludeUnselectedElements.Checked = false;

            cbUseDraco.Checked = false;
            cbUseExtractShell.Checked = false;
            cbGeneratePropDbSqlite.Checked = true;
            cbExportSvfzip.Checked = false;
            cbEnableQuantizedAttributes.Checked = true;
            cbEnableTextureWebP.Checked = true;

            rbModeBasic.Checked = true;
        }

        private void FormExport_Load(object sender, EventArgs e)
        {
            if (!DesignMode)
            {
                InitUI();
            }
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            var filePath = txtTargetPath.Text;

            {
                var dialog = this.folderBrowserDialog1;

                if (string.IsNullOrEmpty(filePath) == false)
                {
                    dialog.SelectedPath = filePath;
                }

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtTargetPath.Text = dialog.SelectedPath;
                }
            }
        }

        private void cbVisualStyle_SelectedIndexChanged(object sender, EventArgs e)
        {
            var visualStyle = cbVisualStyle.SelectedItem as VisualStyleInfo;
            if (visualStyle == null) return;

            foreach (var p in visualStyle.Features)
            {
                _Features.FirstOrDefault(x => x.Type == p.Key)?.ChangeSelected(_Features, p.Value);
            }

            var excludeTexture = _Features.FirstOrDefault(x => x.Type == FeatureType.ExcludeTexture)?.Selected ?? false;
            cbEnableTextureWebP.Enabled = !excludeTexture;
        }

        /// <summary>
        /// 开始导出
        /// </summary>
        /// <param name="uidoc"></param>
        /// <param name="view"></param>
        /// <param name="setting"></param>
        /// <param name="progressCallback"></param>
        /// <param name="cancellationToken"></param>
        private void StartExport(UIDocument uidoc, View3D view, ExportSetting setting,  Action<int> progressCallback, CancellationToken cancellationToken)
        {
            using(var log = new RuntimeLog())
            {
                var exporter = new ExporterX(InnerApp.GetHomePath());
                exporter.Export(
                    view, uidoc, setting,
                    log, progressCallback, cancellationToken
                );
            }
        }

        private void InitUI()
        {
            var config = _LocalConfig;
            if (config.Features != null && config.Features.Count > 0)
            {
                foreach (var featureType in config.Features)
                {
                    _Features.FirstOrDefault(x=>x.Type == featureType)?.ChangeSelected(_Features, true);
                }
            }

            txtTargetPath.Text = config.LastTargetPath;

            bool IsAllowFeature(FeatureType feature)
            {
                return _Features.Any(x => x.Type == feature && x.Selected);
            }

            #region 基本
            {
                //视觉样式
                var visualStyle = _VisualStyles.FirstOrDefault(x => x.Key == config.VisualStyle) ??
                                  _VisualStyleDefault;
                foreach (var p in visualStyle.Features)
                {
                    _Features.FirstOrDefault(x => x.Type == p.Key)?.ChangeSelected(_Features, p.Value);
                }
                cbVisualStyle.SelectedItem = visualStyle;

                //详细程度
                var levelOfDetail = _LevelOfDetails.FirstOrDefault(x => x.Value == config.LevelOfDetail) ??
                                    _LevelOfDetailDefault;
                cbLevelOfDetail.SelectedItem = levelOfDetail;
            }
            #endregion

            #region 排除
            {
                toolTip1.SetToolTip(cbExcludeLines, Strings.FeatureDescriptionExcludeLines);
                toolTip1.SetToolTip(cbExcludeModelPoints, Strings.FeatureDescriptionExcludePoints);
                toolTip1.SetToolTip(cbExcludeUnselectedElements, Strings.FeatureDescriptionOnlySelected);

                if (IsAllowFeature(FeatureType.ExcludeLines))
                {
                    cbExcludeLines.Checked = true;
                }

                if (IsAllowFeature(FeatureType.ExcludePoints))
                {
                    cbExcludeModelPoints.Checked = true;
                }

                if (IsAllowFeature(FeatureType.OnlySelected))
                {
                    cbExcludeUnselectedElements.Checked = true;
                }
            }
            #endregion

            #region 高级
            {
                toolTip1.SetToolTip(cbUseDraco, Strings.FeatureDescriptionUseGoogleDraco);
                toolTip1.SetToolTip(cbUseExtractShell, Strings.FeatureDescriptionExtractShell);
                toolTip1.SetToolTip(cbGeneratePropDbSqlite, Strings.FeatureDescriptionGenerateModelsDb);
                toolTip1.SetToolTip(cbExportSvfzip, Strings.FeatureDescriptionExportSvfzip);
                toolTip1.SetToolTip(cbEnableQuantizedAttributes, Strings.FeatureDescriptionEnableQuantizedAttributes);
                toolTip1.SetToolTip(cbEnableTextureWebP, Strings.FeatureDescriptionEnableTextureWebP);

                if (IsAllowFeature(FeatureType.UseGoogleDraco))
                {
                    cbUseDraco.Checked = true;
                }

                if (IsAllowFeature(FeatureType.ExtractShell))
                {
                    cbUseExtractShell.Checked = true;
                }

                if (IsAllowFeature(FeatureType.GenerateModelsDb))
                {
                    cbGeneratePropDbSqlite.Checked = true;
                }

                if (IsAllowFeature(FeatureType.ExportSvfzip))
                {
                    cbExportSvfzip.Checked = true;
                }

                if (IsAllowFeature(FeatureType.EnableQuantizedAttributes))
                {
                    cbEnableQuantizedAttributes.Checked = true;
                }

                if (IsAllowFeature(FeatureType.EnableTextureWebP))
                {
                    cbEnableTextureWebP.Checked = true;
                }
            }
            #endregion

            #region 3D Tiles

            switch (config.Mode)
            {
                case 0:
                    rbModeBasic.Checked = true;
                    break;
                case 1:
                    rbModeAdvanced.Checked = true;
                    break;
                default:
                    rbModeBasic.Checked = true;
                    break;
            }

            #endregion

            //初始化场地配准信息
            InitSiteLocation(_View.Document);
        }

        private void InitSiteLocation(Document doc)
        {
            var site = ExporterHelper.GetSiteInfo(doc) ?? SiteInfo.CreateDefault();
            txtLongitude.Text = Math.Round(site.Longitude, 6).ToString(CultureInfo.InvariantCulture);
            txtLatitude.Text = Math.Round(site.Latitude, 6).ToString(CultureInfo.InvariantCulture);
            txtHeight.Text = Math.Round(site.Height, 6).ToString(CultureInfo.InvariantCulture);
            txtRotation.Text = Math.Round(site.Rotation, 6).ToString(CultureInfo.InvariantCulture);
        }

        private SiteInfo GetSiteInfo()
        {
            if (TryGetDoubleFromTextBox(txtLongitude, out var lon) &&
                TryGetDoubleFromTextBox(txtLatitude, out var lat) &&
                TryGetDoubleFromTextBox(txtHeight, out var height) &&
                TryGetDoubleFromTextBox(txtRotation, out var rotation))
            {
                return new SiteInfo
                {
                    Longitude = lon,
                    Latitude = lat,
                    Height = height,
                    Rotation = rotation
                };
            }

            return null;
        }

        private bool TryGetDoubleFromTextBox(TextBox tb, out double value)
        {
            if (double.TryParse(tb.Text, out value))
            {
                errorProvider1.SetError(tb, null);
                return true;
            }

            errorProvider1.SetError(tb, Strings.InvalidFormat);
            tb.Focus();
            return false;
        }

        private class VisualStyleInfo
        {
            public string Key { get; }

            private string Text { get; }

            public Dictionary<FeatureType, bool> Features { get; }

            public VisualStyleInfo(string key, string text, Dictionary<FeatureType, bool> features)
            {
                Key = key;
                Text = text;
                Features = features;
            }

            #region Overrides of Object

            public override string ToString()
            {
                return Text;
            }

            #endregion
        }


        private class ComboItemInfo
        {
            public int Value { get;  }

            private string Text { get;  }

            public ComboItemInfo(int value, string text)
            {
                Value = value;
                Text = text;
            }

            #region Overrides of Object

            public override string ToString()
            {
                return Text;
            }

            #endregion
        }

        private void ShowMessageBox(string message)
        {
            ParentForm.ShowMessageBox(message);
        }

        private bool ShowConfigBox(string message)
        {
            return MessageBox.Show(ParentForm, message, ParentForm.Text,
                       MessageBoxButtons.OKCancel,
                       MessageBoxIcon.Question,
                       MessageBoxDefaultButton.Button2) == DialogResult.OK;
        }
    }
}
