# Сторонние компоненты

Исходный код RepairDesk в этом репозитории не сопровождается отдельной разрешительной лицензией. Публичная доступность сама по себе не передает права на использование кода.

Автономная сборка содержит:

| Компонент | Версия | Лицензия / источник |
|---|---|---|
| .NET / WPF runtime | .NET 8, версия фиксируется публикацией | MIT и уведомления в комплекте Microsoft |
| MySqlConnector | 2.4.0 | MIT, https://github.com/mysql-net/MySqlConnector |
| MariaDB Community Server | 11.8.9 winx64 | GPLv2; COPYING, THIRDPARTY и CREDITS находятся в папке server |
| Microsoft Visual C++ runtime | Из официального ZIP MariaDB | Условия распространения Microsoft, уведомления в комплекте MariaDB |
| Microsoft.Web.WebView2 SDK | 1.0.4258.31 | Условия Microsoft, https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4258.31 |
| Onest | Вариативный TTF из Google Fonts | SIL Open Font License 1.1; текст в wwwroot/assets/OFL.txt; https://github.com/google/fonts/tree/main/ofl/onest |

В комплект включён SDK/loader WebView2, но не установленный на этом ПК Evergreen Runtime. Приложение использует существующий runtime или открывает браузер; официальный Fixed Version Runtime можно добавить отдельным параметром публикации. Условия распространения WebView2 необходимо соблюдать отдельно.

Фотографии оборудования получены с Unsplash и включены локально по Unsplash License. Источники и оговорки перечислены в `PHOTOS.md`. Навигационные значки остаются собственными SVG; фотографии категорий не выдают себя за снимки конкретных товаров каталога.

Официальный дистрибутив и контрольные суммы MariaDB: https://dlm.mariadb.com/browse/mariadb_server/11.8.9/winx64-packages/

SHA-256 архива mariadb-11.8.9-winx64.zip:

`830c46727d9278eae212ae3eca44eeb9e71b2a68704e95f344a64fba7b1963f5`

Соответствующие исходники MariaDB 11.8.9 доступны в официальном каталоге: https://archive.mariadb.org/mariadb-11.8.9/source/ и в теге https://github.com/MariaDB/server/tree/mariadb-11.8.9 . При дальнейшем распространении бинарного MariaDB соблюдайте GPLv2 и предоставляйте соответствующие исходники предусмотренным лицензией способом.

Названия производителей, моделей и игр используются для идентификации. Цены каталога — демонстрационные. Источники игровых замеров перечислены отдельно в BENCHMARKS.md.
