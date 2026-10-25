using System.Collections.Generic;
using BimTools.FFTDIM.Core;

namespace BimTools.FFTDIM
{
    /// <summary>
    /// Minimal localisation table for the FFTDIM UI. English is the source of truth;
    /// Arabic translations cover window titles, column headers, buttons and option labels.
    /// Detailed command tooltips and TaskDialog messages remain English in this release —
    /// they are documented in the README instead.
    ///
    /// Usage: <c>L.Apply(settings)</c> once per command (or at ribbon start-up), then
    /// <c>L.T("Key")</c> wherever a caption is needed. Changing the language in
    /// Settings &amp; Options applies to newly opened windows immediately and to the
    /// ribbon after the next Revit start.
    /// </summary>
    public static class L
    {
        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            // Ribbon panels and buttons
            { "PanelQa", "Dimension QA" },
            { "PanelQuick", "Quick Dimension" },
            { "PanelOutput", "Output" },
            { "BtnAudit", "Audit\nDimensions" },
            { "BtnClean", "Clean\nRedundant" },
            { "BtnCoverage", "Coverage\nCheck" },
            { "BtnTolerance", "Tolerance\nCheck" },
            { "BtnBatch", "Batch Audit\nViews" },
            { "BtnSpacing", "Spacing\nChain" },
            { "BtnClearance", "Clearance" },
            { "BtnExport", "Export\nReport" },
            { "BtnBcfExport", "BCF\nExport" },
            { "BtnBcfImport", "BCF\nImport" },
            { "BtnLinkedAudit", "Linked Model\nAudit" },
            { "BtnSettings", "Settings\n& Options" },

            // Results window
            { "ResultsTitleFormat", "FFTDIM — Audit results — {0}" },
            { "ColSeverity", "Severity" },
            { "ColType", "Type" },
            { "ColElement", "Element" },
            { "ColValue", "Value" },
            { "ColView", "View" },
            { "ColDetail", "Detail" },
            { "FilterAll", "All severities" },
            { "FilterError", "Errors only" },
            { "FilterWarning", "Warnings only" },
            { "FilterInfo", "Info only" },
            { "SearchLabel", "Search:" },
            { "SelectInModel", "Select in model" },
            { "ExportCsv", "Export CSV…" },
            { "ExportHtml", "Export HTML…" },
            { "ExportJson", "Export JSON…" },
            { "Close", "Close" },
            { "SaveDialogTitle", "FFTDIM — save audit report" },
            { "ReportSavedFormat", "Report saved to:\n{0}" },
            { "ExportFailedFormat", "Could not write the report:\n\n{0}" },

            // BCF import window
            { "BcfTitleFormat", "FFTDIM — BCF topics — {0}" },
            { "BcfSummaryFormat", "{0} topic(s) · {1} component reference(s) · {2} resolved against the current model\n{3}" },
            { "BcfHint", "Components are matched by Revit element id (AuthoringToolId). References to elements that do not exist in this model stay unresolved — they may belong to a linked model or another project." },
            { "ColTopic", "Topic" },
            { "ColStatus", "Status" },
            { "ColAuthor", "Author" },
            { "ColDate", "Created" },
            { "ColComponents", "Components" },
            { "BcfSelectResolved", "Select in model" },
            { "BcfRefsFormat", "Component references: {0}, resolved against this model: {1}." },
            { "BcfRefsHint", "‘Select in model’ highlights the resolved elements of the selected topic." },
            { "BcfNoResolved", "The selected topic has no components that resolve in the current model." },

            // Settings window
            { "SettingsTitle", "FFTDIM — Settings & Options" },
            { "GroupAudit", "Audit thresholds" },
            { "GroupQuick", "Quick dimension" },
            { "GroupLinkedBcf", "Linked models & BCF" },
            { "GroupReports", "Reports" },
            { "GroupInterface", "Interface" },
            { "OptZeroThreshold", "Zero-value threshold (mm):" },
            { "OptIncludeSpot", "Include spot dimensions in scans" },
            { "OptFlagLinked", "Flag dimensions that reference linked models" },
            { "OptLinkedCoverage", "Check fire-element coverage inside linked models (Linked-Model Audit)" },
            { "OptBcfTopicPerIssue", "BCF export: create one topic per finding (off = one merged topic)" },
            { "OptBcfCamera", "BCF export: embed a viewpoint camera in each topic" },
            { "OptChainOffset", "Spacing chain offset (m):" },
            { "OptChainExtension", "Spacing chain end extension (m):" },
            { "OptClearanceSkip", "Skip clearance below (mm):" },
            { "OptClearanceRadius", "Nearest-target search radius (m):" },
            { "OptPreferWalls", "Prefer walls over grids for clearance" },
            { "OptDefaultFormat", "Default report format:" },
            { "OptLanguage", "Interface language (ribbon updates after Revit restart):" },
            { "Save", "Save" },
            { "Cancel", "Cancel" },
            { "RestoreDefaults", "Restore defaults" },
            { "InvalidNumberFormat", "'{0}' is not a valid number. Please correct it and save again." },
            { "SettingsSavedFormat", "Settings saved to:\n{0}" },

            // View picker window
            { "PickerTitle", "FFTDIM — Batch audit: select views" },
            { "PickerInfo", "Select the views to audit. Each view is audited read-only; results are merged into one window and can be exported together." },
            { "SelectAll", "Select all" },
            { "SelectNone", "Select none" },
            { "RunAudit", "Run audit" },

            // Tolerance window
            { "TolTitle", "FFTDIM — Tolerance rules" }
        };

        private static readonly Dictionary<string, string> Arabic = new Dictionary<string, string>
        {
            // Ribbon panels and buttons
            { "PanelQa", "ضمان جودة الأبعاد" },
            { "PanelQuick", "القياس السريع" },
            { "PanelOutput", "المخرجات" },
            { "BtnAudit", "تدقيق\nالأبعاد" },
            { "BtnClean", "تنظيف\nالمكررات" },
            { "BtnCoverage", "فحص\nالتغطية" },
            { "BtnTolerance", "فحص\nالتفاوتات" },
            { "BtnBatch", "تدقيق عدة\nمناظر" },
            { "BtnSpacing", "سلسلة\nالتباعد" },
            { "BtnClearance", "المسافة الحرة" },
            { "BtnExport", "تصدير\nالتقرير" },
            { "BtnBcfExport", "تصدير\nBCF" },
            { "BtnBcfImport", "استيراد\nBCF" },
            { "BtnLinkedAudit", "تدقيق النماذج\nالمرتبطة" },
            { "BtnSettings", "الإعدادات\nوالخيارات" },

            // Results window
            { "ResultsTitleFormat", "FFTDIM — نتائج التدقيق — {0}" },
            { "ColSeverity", "الشدة" },
            { "ColType", "النوع" },
            { "ColElement", "العنصر" },
            { "ColValue", "القيمة" },
            { "ColView", "المنظر" },
            { "ColDetail", "التفاصيل" },
            { "FilterAll", "كل الشدات" },
            { "FilterError", "الأخطاء فقط" },
            { "FilterWarning", "التحذيرات فقط" },
            { "FilterInfo", "معلومات فقط" },
            { "SearchLabel", "بحث:" },
            { "SelectInModel", "تحديد في النموذج" },
            { "ExportCsv", "تصدير CSV…" },
            { "ExportHtml", "تصدير HTML…" },
            { "ExportJson", "تصدير JSON…" },
            { "Close", "إغلاق" },
            { "SaveDialogTitle", "FFTDIM — حفظ تقرير التدقيق" },
            { "ReportSavedFormat", "تم حفظ التقرير في:\n{0}" },
            { "ExportFailedFormat", "تعذر كتابة التقرير:\n\n{0}" },

            // BCF import window
            { "BcfTitleFormat", "FFTDIM — مواضيع BCF — {0}" },
            { "BcfSummaryFormat", "{0} موضوع · {1} مرجع عنصر · {2} تم التعرف عليه في النموذج الحالي\n{3}" },
            { "BcfHint", "تتم مطابقة العناصر عبر معرّف عنصر Revit‏ (AuthoringToolId). تبقى المراجع التي تشير إلى عناصر غير موجودة في هذا النموذج دون تعريف — قد تكون في نموذج مرتبط أو مشروع آخر." },
            { "ColTopic", "الموضوع" },
            { "ColStatus", "الحالة" },
            { "ColAuthor", "الكاتب" },
            { "ColDate", "أنشئ" },
            { "ColComponents", "العناصر" },
            { "BcfSelectResolved", "تحديد في النموذج" },
            { "BcfRefsFormat", "مراجع العناصر: {0}، تم التعرف عليها في هذا النموذج: {1}." },
            { "BcfRefsHint", "‘تحديد في النموذج’ يبرز العناصر المعرفة للموضوع المحدد." },
            { "BcfNoResolved", "لا تحتوي المواضيع المحددة على عناصر يمكن التعرف عليها في النموذج الحالي." },

            // Settings window
            { "SettingsTitle", "FFTDIM — الإعدادات والخيارات" },
            { "GroupAudit", "حدود التدقيق" },
            { "GroupQuick", "القياس السريع" },
            { "GroupLinkedBcf", "النماذج المرتبطة و BCF" },
            { "GroupReports", "التقارير" },
            { "GroupInterface", "الواجهة" },
            { "OptZeroThreshold", "حد القيمة الصفرية (مم):" },
            { "OptIncludeSpot", "تضمين الأبعاد النقطية في الفحص" },
            { "OptFlagLinked", "الإشارة إلى الأبعاد التي تشير إلى نماذج مرتبطة" },
            { "OptLinkedCoverage", "فحص تغطية عناصر الإطفاء داخل النماذج المرتبطة (تدقيق النماذج المرتبطة)" },
            { "OptBcfTopicPerIssue", "تصدير BCF: موضوع لكل ملاحظة (عند الإيقاف: موضوع واحد مدمج)" },
            { "OptBcfCamera", "تصدير BCF: تضمين كاميرا المنظر في كل موضوع" },
            { "OptChainOffset", "إزاحة سلسلة التباعد (م):" },
            { "OptChainExtension", "امتداد نهاية سلسلة التباعد (م):" },
            { "OptClearanceSkip", "تجاهل المسافات الأقل من (مم):" },
            { "OptClearanceRadius", "نصف قطر البحث عن أقرب هدف (م):" },
            { "OptPreferWalls", "تفضيل الجدران على المحاور عند قياس المسافة" },
            { "OptDefaultFormat", "تنسيق التقرير الافتراضي:" },
            { "OptLanguage", "لغة الواجهة (يتحدّث الشريط بعد إعادة تشغيل Revit):" },
            { "Save", "حفظ" },
            { "Cancel", "إلغاء" },
            { "RestoreDefaults", "استعادة الافتراضية" },
            { "InvalidNumberFormat", "'{0}' ليست قيمة رقمية صحيحة. يرجى التصحيح ثم الحفظ مرة أخرى." },
            { "SettingsSavedFormat", "تم حفظ الإعدادات في:\n{0}" },

            // View picker window
            { "PickerTitle", "FFTDIM — التدقيق الدفعي: اختر المناظر" },
            { "PickerInfo", "اختر المناظر المراد تدقيقها. التدقيق للقراءة فقط؛ تُدمج النتائج في نافذة واحدة ويمكن تصديرها معاً." },
            { "SelectAll", "تحديد الكل" },
            { "SelectNone", "إلغاء التحديد" },
            { "RunAudit", "تشغيل التدقيق" },

            // Tolerance window
            { "TolTitle", "FFTDIM — قواعد التفاوتات" }
        };

        /// <summary>Active language. Set through <see cref="Apply"/>; English until then.</summary>
        public static InterfaceLanguage Language { get; private set; }

        /// <summary>True when the active language is right-to-left.</summary>
        public static bool RightToLeft
        {
            get { return Language == InterfaceLanguage.Arabic; }
        }

        /// <summary>Sets the active language from the persisted settings.</summary>
        public static void Apply(AppSettings settings)
        {
            Language = settings != null ? settings.Language : InterfaceLanguage.English;
        }

        /// <summary>
        /// Translates a key under the active language, falling back to English and
        /// finally to the key itself so a missing translation can never blank the UI.
        /// </summary>
        public static string T(string key)
        {
            if (Language == InterfaceLanguage.Arabic)
            {
                string arabic;
                if (Arabic.TryGetValue(key, out arabic)) return arabic;
            }
            string english;
            if (English.TryGetValue(key, out english)) return english;
            return key;
        }
    }
}
