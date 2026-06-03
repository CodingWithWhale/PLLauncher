using System;
using System.Collections.Generic;

namespace PLLauncher.Services;

public class LocalizationService
{
    public static LocalizationService Instance { get; } = new();

    public event EventHandler? LanguageChanged;

    private string _language = "en-US";

    private static readonly Dictionary<string, Dictionary<string, string>> Strings = new()
    {
        ["en-US"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["confirm.title"] = "Are you sure?",
            ["confirm.yes"] = "Yes",
            ["confirm.cancel"] = "Cancel",
            ["confirm.antisleep"] = "Keep the PC awake by moving the mouse? It stops when you move the mouse yourself.",
            ["confirm.shutdown"] = "Schedule shutdown in 1 hour?",
            ["confirm.lock"] = "Lock this PC now?",
            ["nav.dashboard"] = "Dashboard",
            ["nav.keybinds"] = "Keybinds",
            ["nav.tasks"] = "Tasks",
            ["nav.timelimits"] = "Time Limits",
            ["nav.scheduler"] = "Scheduler",
            ["nav.setups"] = "Setups",
            ["nav.pomodoro"] = "Pomodoro",
            ["nav.appusage"] = "App Usage",
            ["nav.settings"] = "Settings",
            ["settings.title"] = "Settings",
            ["settings.subtitle"] = "Configure PLLauncher preferences",
            ["settings.general"] = "General",
            ["settings.appearance"] = "Appearance",
            ["settings.performance"] = "Performance",
            ["settings.data"] = "Data Management",
            ["settings.language"] = "Language",
            ["settings.save"] = "Save Settings",
            ["settings.saved"] = "Settings saved successfully!",
            ["settings.unsaved"] = "You have unsaved changes",
            ["settings.discard"] = "Discard",
            ["lang.en"] = "English",
            ["lang.ru"] = "Russian",
            ["lang.zh"] = "Chinese (Simplified)",
            ["lang.es"] = "Spanish",
        },
        ["ru-RU"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["confirm.title"] = "Вы уверены?",
            ["confirm.yes"] = "Да",
            ["confirm.cancel"] = "Отмена",
            ["confirm.antisleep"] = "Не давать ПК заснуть, двигая мышь? Режим остановится, когда вы сами подвинете мышь.",
            ["confirm.shutdown"] = "Запланировать выключение через 1 час?",
            ["confirm.lock"] = "Заблокировать этот ПК сейчас?",
            ["nav.dashboard"] = "Панель",
            ["nav.keybinds"] = "Горячие клавиши",
            ["nav.tasks"] = "Задачи",
            ["nav.timelimits"] = "Лимиты времени",
            ["nav.scheduler"] = "Планировщик",
            ["nav.setups"] = "Наборы",
            ["nav.pomodoro"] = "Помодоро",
            ["nav.appusage"] = "Использование",
            ["nav.settings"] = "Настройки",
            ["settings.title"] = "Настройки",
            ["settings.subtitle"] = "Настройка PLLauncher",
            ["settings.general"] = "Общие",
            ["settings.appearance"] = "Внешний вид",
            ["settings.performance"] = "Производительность",
            ["settings.data"] = "Данные",
            ["settings.language"] = "Язык",
            ["settings.save"] = "Сохранить",
            ["settings.saved"] = "Настройки сохранены!",
            ["settings.unsaved"] = "Есть несохранённые изменения",
            ["settings.discard"] = "Отменить",
            ["lang.en"] = "Английский",
            ["lang.ru"] = "Русский",
            ["lang.zh"] = "Китайский (упрощённый)",
            ["lang.es"] = "Испанский",
        },
        ["zh-CN"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["confirm.title"] = "确定吗？",
            ["confirm.yes"] = "是",
            ["confirm.cancel"] = "取消",
            ["confirm.antisleep"] = "通过移动鼠标防止电脑休眠？当您自己移动鼠标时会停止。",
            ["confirm.shutdown"] = "计划在 1 小时后关机？",
            ["confirm.lock"] = "立即锁定此电脑？",
            ["nav.dashboard"] = "仪表板",
            ["nav.keybinds"] = "快捷键",
            ["nav.tasks"] = "任务",
            ["nav.timelimits"] = "时间限制",
            ["nav.scheduler"] = "计划",
            ["nav.setups"] = "启动组",
            ["nav.pomodoro"] = "番茄钟",
            ["nav.appusage"] = "应用使用",
            ["nav.settings"] = "设置",
            ["settings.title"] = "设置",
            ["settings.subtitle"] = "配置 PLLauncher",
            ["settings.general"] = "常规",
            ["settings.appearance"] = "外观",
            ["settings.performance"] = "性能",
            ["settings.data"] = "数据管理",
            ["settings.language"] = "语言",
            ["settings.save"] = "保存设置",
            ["settings.saved"] = "设置已保存！",
            ["settings.unsaved"] = "有未保存的更改",
            ["settings.discard"] = "放弃",
            ["lang.en"] = "英语",
            ["lang.ru"] = "俄语",
            ["lang.zh"] = "简体中文",
            ["lang.es"] = "西班牙语",
        },
        ["es-ES"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["confirm.title"] = "¿Está seguro?",
            ["confirm.yes"] = "Sí",
            ["confirm.cancel"] = "Cancelar",
            ["confirm.antisleep"] = "¿Mantener el PC despierto moviendo el ratón? Se detiene cuando usted lo mueva.",
            ["confirm.shutdown"] = "¿Programar apagado en 1 hora?",
            ["confirm.lock"] = "¿Bloquear este PC ahora?",
            ["nav.dashboard"] = "Panel",
            ["nav.keybinds"] = "Atajos",
            ["nav.tasks"] = "Tareas",
            ["nav.timelimits"] = "Límites",
            ["nav.scheduler"] = "Programador",
            ["nav.setups"] = "Grupos",
            ["nav.pomodoro"] = "Pomodoro",
            ["nav.appusage"] = "Uso de apps",
            ["nav.settings"] = "Ajustes",
            ["settings.title"] = "Ajustes",
            ["settings.subtitle"] = "Configurar PLLauncher",
            ["settings.general"] = "General",
            ["settings.appearance"] = "Apariencia",
            ["settings.performance"] = "Rendimiento",
            ["settings.data"] = "Datos",
            ["settings.language"] = "Idioma",
            ["settings.save"] = "Guardar",
            ["settings.saved"] = "¡Ajustes guardados!",
            ["settings.unsaved"] = "Hay cambios sin guardar",
            ["settings.discard"] = "Descartar",
            ["lang.en"] = "Inglés",
            ["lang.ru"] = "Ruso",
            ["lang.zh"] = "Chino (simplificado)",
            ["lang.es"] = "Español",
        },
    };

    public string CurrentLanguage
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = NormalizeLanguage(value);
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public static string NormalizeLanguage(string code) => code switch
    {
        "ru" or "ru-RU" => "ru-RU",
        "zh" or "zh-CN" => "zh-CN",
        "es" or "es-ES" => "es-ES",
        _ => "en-US"
    };

    public string Get(string key)
    {
        if (Strings.TryGetValue(_language, out var lang) && lang.TryGetValue(key, out var value))
            return value;
        if (Strings["en-US"].TryGetValue(key, out var fallback))
            return fallback;
        return key;
    }

    public void LoadFromSettings(string? languageCode)
        => CurrentLanguage = NormalizeLanguage(languageCode ?? "en-US");
}
