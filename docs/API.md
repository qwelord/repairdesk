# REST API RepairDesk Studio

Базовый адрес переносной версии — `http://127.0.0.1:<автоматический-порт>/api`. Самостоятельный `RepairDesk.Server` по умолчанию слушает порт 5180. Контракт JSON использует camelCase, даты ISO 8601, денежные значения — JSON number, а не строки с валютой.

Вход: `POST /auth/login` с `{ "login": "admin", "password": "RepairDesk2026!" }`. Ответ содержит сотрудника и устанавливает HttpOnly-cookie. Для всех изменяющих запросов, включая вход, передавайте `X-RepairDesk-Client: studio`. Сохраняйте cookie между запросами. CORS намеренно выключен.

## Ресурсы

| Ресурс | Маршруты |
|---|---|
| Сессия | GET `/auth/me`, POST `/auth/login`, POST `/auth/logout`, PUT `/auth/password` |
| Справочные значения | GET `/reference`, GET `/games`, GET `/staff`, POST `/staff` |
| Клиенты | GET `/clients`, GET `/clients/{id}`, POST `/clients`, PUT `/clients/{id}`, DELETE `/clients/{id}` |
| Комплектующие | GET `/parts`, GET `/parts/{id}`, POST `/parts`, PUT `/parts/{id}`, DELETE `/parts/{id}` |
| Склад | POST `/parts/{id}/movements`, GET `/stock-movements` |
| Услуги | GET `/services`, GET `/services/{id}`, POST `/services`, PUT `/services/{id}`, DELETE `/services/{id}` |
| Заявки | GET `/orders`, GET `/orders/{id}`, POST `/orders`, PUT `/orders/{id}`, DELETE `/orders/{id}?version=N` |
| Позиции заказа | POST `/orders/{id}/lines`, DELETE `/orders/{id}/lines/{lineId}?version=N` |
| Платежи | POST `/orders/{id}/payments` |
| Заметки | POST `/orders/{id}/notes` |
| Диагностика | PUT `/orders/{id}/diagnostics/{key}` |
| Сметы | POST `/orders/{id}/estimates`, GET `/orders/{id}/estimates/{estimateId}`, PUT `/orders/{id}/estimates/{estimateId}` |
| Сохранённые сборки | GET `/builds`, POST `/builds`, GET `/builds/{id}/parts`, DELETE `/builds/{id}` |
| Анализ и подбор | POST `/builds/evaluate`, POST `/builds/recommend`, POST `/builds/compare`, POST `/builds/order` |
| Выгрузки | GET `/exports/orders.csv`, GET `/exports/backup.sql` (только администратор) |
| Проверка запуска | GET `/health` (без сессии) |

DELETE заявки выполняет контролируемую отмену с возвратом деталей и сохранением истории; уничтожения финансового документа нет. Используемые клиенты и детали защищены внешними ключами. Складское количество в POST/PUT комплектующей не устанавливается: используйте отдельное движение.

## Пример PowerShell

```powershell
$base = 'http://127.0.0.1:5180/api'
$headers = @{ 'X-RepairDesk-Client' = 'studio' }
$login = @{ login='admin'; password='RepairDesk2026!' } | ConvertTo-Json
Invoke-RestMethod "$base/auth/login" -Method Post -ContentType 'application/json' `
  -Headers $headers -Body $login -SessionVariable session
Invoke-RestMethod "$base/clients" -WebSession $session
$client = @{ name='Тестовый клиент'; phone='79001112233'; email=''; notes='Создан через API' } | ConvertTo-Json
Invoke-RestMethod "$base/clients" -Method Post -ContentType 'application/json' `
  -Headers $headers -WebSession $session -Body ([Text.Encoding]::UTF8.GetBytes($client))
```

Создание клиента возвращает 201, JSON `{ "id": N }` и Location. PUT/DELETE при успехе возвращают 204.

## Основные DTO

```json
{
  "clientId": 1,
  "deviceType": "ПК",
  "device": "Рабочая станция",
  "serial": "RD-DEMO-01",
  "complaint": "Нестабильная работа",
  "condition": "Корпус и кабель питания",
  "dueAt": "2026-10-24",
  "technicianId": 3
}
```

Для PUT заявки: `version`, `diagnosis`, `dueAt`, `technicianId`, `status`. Версию берите из свежего GET `/orders/{id}`. Его ответ включает `order`, `lines`, `events`, `estimates`, `diagnostics`, `nextStatuses`.

Для строки заказа: `{ "version": 1, "partId": 12, "serviceId": null, "quantity": 1 }`. Заполните ровно один из partId/serviceId.

Для сметы: `{ "version": 1, "note": "После диагностики", "items": [{ "partId": 12, "serviceId": null, "quantity": 1 }] }`.

Для решения по смете: `{ "version": 2, "status": "Согласована", "approvedBy": "Имя подтвердившего решение клиента" }`. Следующее действие — `status: "Применена"`; отклонение — `"Отклонена"`. Это запись сотрудником, не электронная подпись клиента.

Для диагностики: `{ "version": 1, "result": "Проблема", "note": "SMART: ошибки носителя" }`. Ключи и допустимые этапы доступны через `/reference`.

Для движения: `{ "quantity": 2, "reason": "Поступление" }`. Отрицательное quantity означает списание. Для платежа: `{ "version": 1, "amount": 1500 }`; отрицательная сумма — возврат.

Для анализа сборки: `{ "partIds": [1,4,7,10,14,16,12], "width": 1920 }`. Сохранение дополнительно принимает `name`, `clientId`. Подбор: `{ "budget": 100000, "width": 1920, "inStockOnly": true }`. Сравнение: `{ "current": [1,4,7,10,14,16,12], "proposed": [2,5,8,11,14,16,12], "width": 2560 }`.

## Ошибки и безопасность

- 400 — неверный JSON, обязательное поле или Host.
- 401 — отсутствует сессия / неверный пароль.
- 403 — запрещённый источник или нет специального заголовка.
- 404 — отсутствующий ресурс одиночного чтения / неизвестный маршрут.
- 409 — нарушено бизнес-правило: роль, версия, остаток, связь или статус.
- 429 — слишком много попыток входа.
- 500 — техническая ошибка без выдачи SQL, стека и паролей клиенту.

Ошибка содержит `title`, `status`, обычно `traceId`. Клиент показывает сообщение, а не считает любую ошибку успешным сохранением. API проверяет права и целостность независимо от интерфейса. Для публичного внешнего размещения эта локальная конфигурация не подходит без отдельного аудита.
