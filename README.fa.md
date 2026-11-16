<div dir="rtl">

# برنامه DNS Party — مدیریت حرفه‌ای DNS ویندوز

[![English Description](https://img.shields.io/badge/%F0%9F%87%BA%F0%9F%87%B8_English-English_Description-0969da?style=for-the-badge)](README.md)
[![Latest Release](https://img.shields.io/github/v/release/emviux/dns-party?style=for-the-badge&label=Download&color=7c3aed)](https://github.com/emviux/dns-party/releases/latest)

**DNS Party** یک ابزار بومی، سریع، سبک و با الهام از محیط گیمینگ برای دسکتاپ ویندوز است که مدیریت، تست پینگ و جابجایی بین تنظیمات سرورهای DNS نسخه IPv4 و IPv6 را تنها با یک کلیک از طریق **System Tray** (نوار وظیفه) و **منوی درون‌بازی (In-Game Overlay)** فراهم می‌کند.

---

## دانلود و روش‌های استفاده (۳ روش)

شما می‌توانید به هر یک از **۳ روش** زیر برنامه **DNS Party** را دریافت و استفاده کنید:

### ۱. نصب از طریق جادوگر نصب — Install Wizard (پیشنهادی)
1. فایل **[`DNSParty-Setup.exe`](https://github.com/emviux/dns-party/raw/main/dist/DNSParty-Setup.exe)** را دانلود کنید (همچنین در بخش [**GitHub Releases**](https://github.com/emviux/dns-party/releases/latest) در دسترس است).
2. فایل نصبی را اجرا کنید، مسیر دلخواه خود را انتخاب نمایید (برنامه به طور خودکار پوشه `DNS Party` را در مسیر انتخابی می‌سازد) و مراحل نصب را تکمیل کنید.
3. میانبرهای دسکتاپ و منوی استارت را ایجاد می‌کند، تنظیمات شما را در مسیر `%LocalAppData%\DNSParty` در زمان آپدیت حفظ می‌کند و فایل حذف‌کننده اختصاصی (`Unins000.exe`) را در بخش **Installed Apps** و **Control Panel** ویندوز ثبت می‌نماید.
   - *پشتیبانی از نصب بی‌صدا (Silent):* `DNSParty-Setup.exe /S`

### ۲. نسخه پرتابل — Portable (بدون نیاز به نصب)
1. فایل **[`DNSParty-Portable.exe`](https://github.com/emviux/dns-party/raw/main/dist/DNSParty-Portable.exe)** را دانلود کنید (همچنین در بخش [**GitHub Releases**](https://github.com/emviux/dns-party/releases/latest) در دسترس است).
2. فایل `DNSParty-Portable.exe` را مستقیماً از هر پوشه‌ای اجرا کنید — بدون نیاز به هیچ‌گونه نصب.
3. *حالت فلش مموری / کاملاً پرتابل (اختیاری):* اگر یک فایل خالی با نام `portable.flag` در کنار فایل `DNSParty-Portable.exe` بسازید، تمام تنظیمات و پروفایل‌های سفارشی DNS مستقیماً در کنار فایل اجرایی در `portable_config.json` ذخیره می‌شوند.

### ۳. ساخت از سورس کد با `build.bat` (مخصوص کاربران فنی و توسعه‌دهندگان)
اگر تمایل دارید سورس کد را بررسی کرده و خودتان برنامه را از صفر کامپایل کنید:
1. اطمینان حاصل کنید که روی **ویندوز ۱۰ یا ویندوز ۱۱** هستید و **Visual Studio** (یا **Visual Studio Build Tools** با پشتیبانی از C# / MSBuild) روی سیستم نصب است.
2. مخزن را کلون کرده و اسکریپت `build.bat` را اجرا کنید:

<div dir="ltr">

```bat
git clone https://github.com/emviux/dns-party.git
cd dns-party
.\build.bat
```

</div>

3. اسکریپت `build.bat` به صورت خودکار کامپایلر Roslyn C# (`csc.exe`) را پیدا کرده، برنامه اصلی، حذف‌کننده اختصاصی (`Unins000.exe`) و جادوگر نصب را در پوشه `dist\DNSParty-Portable.exe` و `dist\DNSParty-Setup.exe` کامپایل می‌کند و تست‌های کنترل کیفیت خودکار را اجرا می‌نماید.

---

## ویژگی‌های کلیدی

- **تغییر DNS با یک کلیک**: جابجایی آنی بین **Cloudflare** (`1.1.1.1`)، **Google DNS** (`8.8.8.8`)، **Quad9** (`9.9.9.9`)، **AdGuard DNS** (`94.140.14.14`)، **OpenDNS** (`208.67.222.222`) و بی‌نهایت پروفایل اختصاصی IPv4 و IPv6.
- **بررسی وضعیت واقعی شبکه ویندوز**: وضعیت واقعی آداپتور شبکه ویندوز (`NetworkInterface` و رجیستری `Tcpip(6)`) را می‌خواند تا تفاوت بین DNS درخواستی و DNS فعال واقعی را تشخیص دهد و هرگز وضعیت اتصال کاذب نمایش ندهد.
- **منوی همپوشانی درون بازی (In-Game Overlay)**: پنجره شفاف و بدون حاشیه برای تغییر سریع DNS با کلید میانبر سراسری (پیش‌فرض `Shift+Tab` و قابل تغییر در تنظیمات) بدون نیاز به خروج از محیط بازی.
- **غیرفعال‌سازی DNS (بازگشت به DHCP خودکار)**: آداپتور فعال وای‌فای یا اترنت را به حالت دریافت خودکار DNS ویندوز (`source=dhcp`) بازمی‌گرداند و صحت بازگشت را تأیید می‌کند.
- **پاکسازی کش DNS (Flush DNS Cache)**: دستور `ipconfig /flushdns` را کاملاً مخفی و بدون باز شدن پنجره سیاه CMD اجرا کرده و نتیجه را نمایش می‌دهد.
- **تست دقیق پینگ و تأخیر DNS از طریق پورت UDP 53**: زمان پاسخگویی واقعی سرورهای DNS را بر حسب میلی‌ثانیه (`ms`) با ارسال بسته استاندارد RFC 1035 اندازه‌گیری می‌کند.
- **اجرا در پس‌زمینه و System Tray**:
  - هنگام بستن پنجره از شما می‌پرسد که آیا برنامه در پس‌زمینه فعال بماند یا کاملاً بسته شود (با قابلیت به خاطر سپردن انتخاب شما).
  - در حالت مخفی در System Tray، از منوی `Alt+Tab` پنهان می‌شود (`WS_EX_TOOLWINDOW`) و مصرف پردازنده (CPU) در حالت بیکار نزدیک به ۰٪ است.
  - منوی کلیک راست روی آیکون Tray امکان تغییر فوری DNS، مشاهده تیک DNS فعال، زیرمنوی **علاقه‌مندی‌ها (Favorites)**، **غیرفعال کردن DNS**، **پاکسازی کش DNS**، **تنظیمات** و **خروج** را فراهم می‌کند.
- **به‌روزرسانی خودکار و نمایش تغییرات نسخه جدید**: انتشار نسخه‌های جدید در GitHub Releases را بررسی کرده و پس از نصب آپدیت، فهرست امکانات جدید و باگ‌های برطرف‌شده را نمایش می‌دهد.
- **افکت‌های صوتی ظریف هنگام اتصال**: هنگام تغییر موفق DNS، افکت صوتی کوتاه `.wav` (`connect.wav` / `disconnect.wav`) پخش می‌کند (قابل فعال/غیرفعال‌سازی در تنظیمات).
- **مدیریت هوشمند دسترسی ادمین (UAC)**: رابط کاربری برنامه با سطح دسترسی کاربر عادی (`asInvoker`) اجرا می‌شود تا هنگام باز کردن برنامه نیازی به تأیید UAC نباشد؛ و فقط در لحظه اعمال تغییر DNS روی کارت شبکه به صورت هوشمند دسترسی ادمین درخواست می‌کند.

---

## ساختار پروژه

<div dir="ltr">

```text
DNS Party/
├── ARCHITECTURE.md                  # Architecture, stack benchmarks & design decisions
├── README.md                        # English documentation & installation guide
├── README.fa.md                     # Persian (RTL) documentation & installation guide
├── build.bat                        # 1-click source build & automated QA test script
├── assets/                          # Application icons, branding & tray icon pack
├── src/
│   ├── DNSParty/
│   │   ├── DNSParty.csproj          # MSBuild project file (.NET Framework 4.8)
│   │   ├── app.manifest             # PerMonitorV2 DPI & UAC manifest
│   │   ├── Program.cs               # Single-instance entry point & CLI elevation helper
│   │   ├── VersionInfo.cs           # Application version & repository metadata
│   │   ├── Models/
│   │   │   ├── DnsProfile.cs        # DNS profile model, defaults & strict IPv4/IPv6 validation
│   │   │   ├── AppSettings.cs       # User settings & profile persistence model
│   │   │   └── DnsStatusSnapshot.cs # Live Windows network adapter DNS state snapshot
│   │   ├── Services/
│   │   │   ├── ConfigService.cs     # Atomic JSON persistence & corruption recovery
│   │   │   ├── DnsService.cs        # Adapter detection, netsh DNS apply/DHCP, flush & UDP test
│   │   │   ├── HotkeyService.cs     # Global hotkey registration for In-Game Overlay
│   │   │   ├── SoundService.cs      # Local WAV generator & non-blocking audio player
│   │   │   └── UpdateService.cs     # GitHub Releases update checker & installer launcher
│   │   └── UI/
│   │       ├── Theme.cs             # Dark gaming navy/purple & Light theme palette + GDI+ helpers
│   │       ├── IconRenderer.cs      # Vector icon & provider logo renderer + multi-DPI tray icons
│   │       ├── TrayMenuRenderer.cs  # Custom dark context menu renderer for System Tray
│   │       ├── AddEditDnsDialog.cs  # Modal dialog for adding/editing IPv4 & IPv6 DNS profiles
│   │       ├── OverlayForm.cs       # Borderless In-Game DNS switching overlay
│   │       └── MainForm.cs          # Main dashboard, Favorites, Settings, About & Tray controller
│   └── Installer/
│       ├── InstallerProgram.cs      # Interactive GUI & silent Windows Setup Wizard
│       └── UninstallerProgram.cs    # Dedicated Windows Uninstaller (Unins000.exe)
├── tests/
│   └── TestRunner.cs                # Automated QA & verification suite
└── dist/
    ├── DNSParty-Setup.exe           # Method 1: Standalone Windows Install Wizard
    └── DNSParty-Portable.exe        # Method 2: Standalone Single-File Portable Executable
```

</div>

---

## مجوز انتشار (License)

این پروژه تحت مجوز [MIT License](LICENSE) منتشر شده است.

</div>
