USE repairdesk;
START TRANSACTION;
UPDATE games SET name='Cyberpunk 2077 1.6' WHERE id=4;
SET @review='https://www.notebookcheck.net/KFA2-GeForce-RTX-4060-1-Click-OC-2X-graphics-card-review-Current-entry-level-GPU-uses-more-power-than-expected.736269.0.html';
INSERT INTO benchmarks(game_id,gpu_id,test_cpu,width,height,preset,ray_tracing,upscaling,average_fps,source_title,source_url,test_date,notes) VALUES
(2,7,'AMD Ryzen 9 7950X',1920,1080,'Ultra (3/3), best looking','Нет','Нет',228,'Notebookcheck: KFA2 RTX 4060',@review,'2026-10-21','Результат Dota 2 Reborn из обзора 2023 года. Дата записи — дата проверки источника, не дата замера. Версии Dota 2 существенно меняются.'),
(2,7,'AMD Ryzen 9 7950X',3840,2160,'Ultra (3/3), best looking','Нет','Нет',144.8,'Notebookcheck: KFA2 RTX 4060',@review,'2026-10-21','Исторический тест Dota 2 Reborn, не прогноз для текущего патча.'),
(4,7,'AMD Ryzen 9 7950X',1920,1080,'Ultra, версия 1.6','Выключен','Выключено',79.9,'Notebookcheck: KFA2 RTX 4060',@review,'2026-10-21','Версия 1.6. Не переносить на версии 2.x и Phantom Liberty.'),
(4,7,'AMD Ryzen 9 7950X',2560,1440,'Ultra, версия 1.6','Выключен','Выключено',47.7,'Notebookcheck: KFA2 RTX 4060',@review,'2026-10-21','Версия 1.6. Не переносить на версии 2.x и Phantom Liberty.'),
(4,7,'AMD Ryzen 9 7950X',3840,2160,'Ultra, версия 1.6','Выключен','Выключено',20.4,'Notebookcheck: KFA2 RTX 4060',@review,'2026-10-21','Версия 1.6. Не переносить на версии 2.x и Phantom Liberty.'),
(6,7,'AMD Ryzen 9 7950X',1920,1080,'Ultra','Нет','Выключено',70,'Notebookcheck: KFA2 RTX 4060',@review,'2026-10-21','God of War (2018), ПК-релиз 2022. Не Ragnarok.'),
(6,7,'AMD Ryzen 9 7950X',2560,1440,'Ultra','Нет','Выключено',57.2,'Notebookcheck: KFA2 RTX 4060',@review,'2026-10-21','God of War (2018), ПК-релиз 2022. Не Ragnarok.'),
(6,7,'AMD Ryzen 9 7950X',3840,2160,'Ultra','Нет','Выключено',37.3,'Notebookcheck: KFA2 RTX 4060',@review,'2026-10-21','God of War (2018), ПК-релиз 2022. Не Ragnarok.'),
(5,7,'AMD Ryzen 9 7950X',1920,1080,'Highest, 16xAF, 4xMSAA + FXAA','Нет','Нет',127.8,'Notebookcheck: KFA2 RTX 4060',@review,'2026-10-21','GTA V Legacy. Не Enhanced. В 4K источник использует другой пресет.'),
(5,7,'AMD Ryzen 9 7950X',2560,1440,'Highest, 16xAF, 4xMSAA + FXAA','Нет','Нет',90.3,'Notebookcheck: KFA2 RTX 4060',@review,'2026-10-21','GTA V Legacy. Не Enhanced.'),
(5,7,'AMD Ryzen 9 7950X',3840,2160,'High, Advanced Graphics off, FXAA','Нет','Нет',88.9,'Notebookcheck: KFA2 RTX 4060',@review,'2026-10-21','Внимание: High в 4K отличается от Highest с MSAA в меньших разрешениях.');
SET @witcher='https://www.notebookcheck.net/The-Witcher-3-v4-00-Notebook-and-desktop-benchmarks.683164.0.html';
INSERT INTO benchmarks(game_id,gpu_id,test_cpu,width,height,preset,ray_tracing,upscaling,average_fps,source_title,source_url,test_date,notes) VALUES
(1,7,'Разные стенды Notebookcheck, агрегат',1920,1080,'Ultra, FXAA','Выключен','Выключено',117.25,'Notebookcheck: The Witcher 3 v4',@witcher,'2026-10-21','Среднее опубликованных результатов настольной RTX 4060. Тесты на разных CPU.'),
(1,7,'Разные стенды Notebookcheck, агрегат',2560,1440,'Ultra, FXAA','Выключен','Выключено',80.75,'Notebookcheck: The Witcher 3 v4',@witcher,'2026-10-21','Среднее опубликованных результатов настольной RTX 4060.'),
(1,7,'Разные стенды Notebookcheck, агрегат',3840,2160,'Ultra, FXAA','Выключен','Выключено',44,'Notebookcheck: The Witcher 3 v4',@witcher,'2026-10-21','Среднее опубликованных результатов настольной RTX 4060.'),
(1,8,'Разные стенды Notebookcheck, агрегат',1920,1080,'Ultra, FXAA','Выключен','Выключено',156.8,'Notebookcheck: The Witcher 3 v4',@witcher,'2026-10-21','Среднее результатов настольной RTX 4070.'),
(1,8,'Разные стенды Notebookcheck, агрегат',2560,1440,'Ultra, FXAA','Выключен','Выключено',108.45,'Notebookcheck: The Witcher 3 v4',@witcher,'2026-10-21','Среднее результатов настольной RTX 4070.'),
(1,8,'Разные стенды Notebookcheck, агрегат',3840,2160,'Ultra, FXAA','Выключен','Выключено',56.35,'Notebookcheck: The Witcher 3 v4',@witcher,'2026-10-21','Среднее результатов настольной RTX 4070.'),
(1,9,'Стенд Notebookcheck, см. источник',1920,1080,'Ultra, FXAA','Выключен','Выключено',121.3,'Notebookcheck: The Witcher 3 v4',@witcher,'2026-10-21','Результат настольной RX 7600.'),
(1,9,'Стенд Notebookcheck, см. источник',2560,1440,'Ultra, FXAA','Выключен','Выключено',77.3,'Notebookcheck: The Witcher 3 v4',@witcher,'2026-10-21','Результат настольной RX 7600.'),
(1,9,'Стенд Notebookcheck, см. источник',3840,2160,'Ultra, FXAA','Выключен','Выключено',40.1,'Notebookcheck: The Witcher 3 v4',@witcher,'2026-10-21','Результат настольной RX 7600.');
SET @wukong='https://www.notebookcheck.net/Black-Myth-Wukong-Our-benchmarks-of-the-new-Unreal-Engine-5-game.876868.0.html';
INSERT INTO benchmarks(game_id,gpu_id,test_cpu,width,height,preset,ray_tracing,upscaling,average_fps,source_title,source_url,test_date,notes) VALUES
(8,7,'Разные стенды Notebookcheck, агрегат',1920,1080,'Cinematic','Выключен','TSR 100%, FG выключена',27,'Notebookcheck: Black Myth Wukong',@wukong,'2026-10-21','Встроенный benchmark tool. Не гарантирует такой же FPS в боях.'),
(8,7,'Разные стенды Notebookcheck, агрегат',2560,1440,'Cinematic','Выключен','TSR 100%, FG выключена',20,'Notebookcheck: Black Myth Wukong',@wukong,'2026-10-21','Встроенный benchmark tool.'),
(8,7,'Разные стенды Notebookcheck, агрегат',3840,2160,'Cinematic','Выключен','TSR 100%, FG выключена',9.5,'Notebookcheck: Black Myth Wukong',@wukong,'2026-10-21','Встроенный benchmark tool.'),
(8,8,'Стенд Notebookcheck, см. источник',1920,1080,'Cinematic','Выключен','TSR 100%, FG выключена',45,'Notebookcheck: Black Myth Wukong',@wukong,'2026-10-21','Встроенный benchmark tool.'),
(8,8,'Стенд Notebookcheck, см. источник',2560,1440,'Cinematic','Выключен','TSR 100%, FG выключена',34,'Notebookcheck: Black Myth Wukong',@wukong,'2026-10-21','Встроенный benchmark tool.'),
(8,8,'Стенд Notebookcheck, см. источник',3840,2160,'Cinematic','Выключен','TSR 100%, FG выключена',19,'Notebookcheck: Black Myth Wukong',@wukong,'2026-10-21','Встроенный benchmark tool.');
SET @cs='https://www.notebookcheck.net/Counter-Strike-2-in-review-laptop-and-desktop-benchmarks.767018.0.html';
INSERT INTO benchmarks(game_id,gpu_id,test_cpu,width,height,preset,ray_tracing,upscaling,average_fps,source_title,source_url,test_date,notes) VALUES
(3,9,'Стенд Notebookcheck, см. источник',1920,1080,'Very High','Нет','Выключено',138,'Notebookcheck: Counter-Strike 2',@cs,'2026-10-21','Исторический тест CS2. Результат чувствителен к CPU, карте и патчу.'),
(3,9,'Стенд Notebookcheck, см. источник',2560,1440,'Very High','Нет','Выключено',89.6,'Notebookcheck: Counter-Strike 2',@cs,'2026-10-21','Исторический тест CS2.'),
(3,9,'Стенд Notebookcheck, см. источник',3840,2160,'Very High','Нет','Выключено',44.1,'Notebookcheck: Counter-Strike 2',@cs,'2026-10-21','Исторический тест CS2.');
SET @csbase='https://www.computerbase.de/2023-09/counter-strike-2-cs2-benchmark-test/2/';
INSERT INTO benchmarks(game_id,gpu_id,test_cpu,width,height,preset,ray_tracing,upscaling,average_fps,low_fps,source_title,source_url,test_date,notes) VALUES
(3,7,'AMD Ryzen 9 7950X3D, 32GB DDR5-6000',1920,1080,'Very High, MSAA 8x','Нет','Выключено',165,113.7,'ComputerBase: Counter-Strike 2',@csbase,'2023-09-28','Сцена Ancient, тест 2023 года. Low FPS — 1-й перцентиль. Результат не относится к текущему патчу.'),
(3,7,'AMD Ryzen 9 7950X3D, 32GB DDR5-6000',2560,1440,'Very High, MSAA 8x','Нет','Выключено',105.6,72.5,'ComputerBase: Counter-Strike 2',@csbase,'2023-09-28','Сцена Ancient, тест 2023 года. Low FPS — 1-й перцентиль.'),
(3,7,'AMD Ryzen 9 7950X3D, 32GB DDR5-6000',3840,2160,'Very High, MSAA 4x','Нет','Выключено',67.5,44.3,'ComputerBase: Counter-Strike 2',@csbase,'2023-09-28','В 4K используется MSAA 4x; сравнивать с 8x в FHD/QHD следует с учетом разных настроек.'),
(3,8,'AMD Ryzen 9 7950X3D, 32GB DDR5-6000',1920,1080,'Very High, MSAA 8x','Нет','Выключено',252.1,170.8,'ComputerBase: Counter-Strike 2',@csbase,'2023-09-28','Сцена Ancient. Исторический тест.'),
(3,8,'AMD Ryzen 9 7950X3D, 32GB DDR5-6000',2560,1440,'Very High, MSAA 8x','Нет','Выключено',166.1,110.6,'ComputerBase: Counter-Strike 2',@csbase,'2023-09-28','Сцена Ancient. Исторический тест.'),
(3,8,'AMD Ryzen 9 7950X3D, 32GB DDR5-6000',3840,2160,'Very High, MSAA 4x','Нет','Выключено',107.8,66.7,'ComputerBase: Counter-Strike 2',@csbase,'2023-09-28','Сцена Ancient. В 4K MSAA 4x.');
INSERT INTO benchmarks(game_id,gpu_id,test_cpu,width,height,preset,ray_tracing,upscaling,average_fps,source_title,source_url,test_date,notes) VALUES
(9,7,'Стенд Technopat, см. источник',1920,1080,'Ultra','Нет','В таблице не указано',76,'Technopat: NVIDIA RTX 4060','https://www.technopat.net/2023/06/28/nvidia-geforce-rtx-4060-incelemesi/','2023-06-28','RDR2: среднее 76, минимум 49 FPS в обзоре. Набор отдельных настроек и версия игры влияют на сравнение.');
INSERT INTO games(id,name) VALUES (10,'Hogwarts Legacy'),(11,'Far Cry 5'),(12,'Far Cry 6'),(13,'Metro Exodus (2019)'),(14,'Tiny Tina’s Wonderlands');
SET @review='https://www.notebookcheck.net/KFA2-GeForce-RTX-4060-1-Click-OC-2X-graphics-card-review-Current-entry-level-GPU-uses-more-power-than-expected.736269.0.html';
INSERT INTO benchmarks(game_id,gpu_id,test_cpu,width,height,preset,ray_tracing,upscaling,average_fps,source_title,source_url,test_date,notes) VALUES
(10,7,'AMD Ryzen 9 7950X, 32GB DDR5-6000',1920,1080,'Ultra, AF High, TAA','Не указан','Не указано',66.5,'Notebookcheck: KFA2 RTX 4060',@review,'2023-07-25','Опубликованный тест KFA2 RTX 4060 OC. Исполнение MSI из каталога может отличаться.'),
(10,7,'AMD Ryzen 9 7950X, 32GB DDR5-6000',2560,1440,'Ultra, AF High, TAA','Не указан','Не указано',46.3,'Notebookcheck: KFA2 RTX 4060',@review,'2023-07-25','Исторический тест 2023 года.'),
(10,7,'AMD Ryzen 9 7950X, 32GB DDR5-6000',3840,2160,'Ultra, AF High, TAA','Не указан','Не указано',24.9,'Notebookcheck: KFA2 RTX 4060',@review,'2023-07-25','Исторический тест 2023 года.'),
(11,7,'AMD Ryzen 9 7950X, 32GB DDR5-6000',1920,1080,'Ultra, TAA','Нет','Не указано',162,'Notebookcheck: KFA2 RTX 4060',@review,'2023-07-25','Встроенный бенчмарк.'),
(11,7,'AMD Ryzen 9 7950X, 32GB DDR5-6000',2560,1440,'Ultra, TAA','Нет','Не указано',110,'Notebookcheck: KFA2 RTX 4060',@review,'2023-07-25','Встроенный бенчмарк.'),
(11,7,'AMD Ryzen 9 7950X, 32GB DDR5-6000',3840,2160,'Ultra, TAA','Нет','Не указано',55,'Notebookcheck: KFA2 RTX 4060',@review,'2023-07-25','Встроенный бенчмарк.'),
(12,7,'AMD Ryzen 9 7950X, 32GB DDR5-6000',1920,1080,'Ultra + HD Textures, TAA','Нет','Не указано',122.1,'Notebookcheck: KFA2 RTX 4060',@review,'2023-07-25','Отдельная таблица DXR не используется.'),
(12,7,'AMD Ryzen 9 7950X, 32GB DDR5-6000',2560,1440,'Ultra + HD Textures, TAA','Нет','Не указано',85.4,'Notebookcheck: KFA2 RTX 4060',@review,'2023-07-25','Отдельная таблица DXR не используется.'),
(12,7,'AMD Ryzen 9 7950X, 32GB DDR5-6000',3840,2160,'Ultra + HD Textures, TAA','Нет','Не указано',40.9,'Notebookcheck: KFA2 RTX 4060',@review,'2023-07-25','Отдельная таблица DXR не используется.'),
(13,7,'AMD Ryzen 9 7950X, 32GB DDR5-6000',1920,1080,'Ultra, 16xAF','Нет','Не указано',80.2,'Notebookcheck: KFA2 RTX 4060',@review,'2023-07-25','Оригинальная версия, не Enhanced Edition.'),
(13,7,'AMD Ryzen 9 7950X, 32GB DDR5-6000',2560,1440,'Ultra, 16xAF','Нет','Не указано',62.9,'Notebookcheck: KFA2 RTX 4060',@review,'2023-07-25','Оригинальная версия, не Enhanced Edition.'),
(13,7,'AMD Ryzen 9 7950X, 32GB DDR5-6000',3840,2160,'Ultra, 16xAF','Нет','Не указано',38.3,'Notebookcheck: KFA2 RTX 4060',@review,'2023-07-25','Оригинальная версия, не Enhanced Edition.'),
(14,7,'AMD Ryzen 9 7950X, 32GB DDR5-6000',1920,1080,'Badass, DX12','Не указан','Не указано',92.4,'Notebookcheck: KFA2 RTX 4060',@review,'2023-07-25','Исторический тест 2023 года.'),
(14,7,'AMD Ryzen 9 7950X, 32GB DDR5-6000',2560,1440,'Badass, DX12','Не указан','Не указано',62.6,'Notebookcheck: KFA2 RTX 4060',@review,'2023-07-25','Исторический тест 2023 года.'),
(14,7,'AMD Ryzen 9 7950X, 32GB DDR5-6000',3840,2160,'Badass, DX12','Не указан','Не указано',26.6,'Notebookcheck: KFA2 RTX 4060',@review,'2023-07-25','Исторический тест 2023 года.');
SET @rxreview='https://www.notebookcheck.net/AMD-Radeon-RX-7600-desktop-GPU-review-RDNA3-vs-Ada-Lovelace-in-the-entry-level-segment.744635.0.html';
INSERT INTO benchmarks(game_id,gpu_id,test_cpu,width,height,preset,ray_tracing,upscaling,average_fps,source_title,source_url,test_date,notes) VALUES
(2,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',1920,1080,'Ultra (3/3), best looking','Нет','Нет',168.5,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Результаты XFX QICK 308 OC; другое исполнение RX 7600 может отличаться.'),
(2,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',3840,2160,'Ultra (3/3), best looking','Нет','Нет',108.8,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Исторический тест.'),
(4,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',1920,1080,'Ultra','Нет','Не указано',89.9,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Версия игры 1.6.'),
(4,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',2560,1440,'Ultra','Нет','Не указано',55.9,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Версия игры 1.6.'),
(4,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',3840,2160,'Ultra','Нет','Не указано',24,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Версия игры 1.6.'),
(5,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',1920,1080,'Highest, 16xAF, 4xMSAA + FXAA','Нет','Нет',112.9,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','GTA V Legacy, не Enhanced.'),
(5,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',2560,1440,'Highest, 16xAF, 4xMSAA + FXAA','Нет','Нет',77.4,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','GTA V Legacy, не Enhanced.'),
(5,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',3840,2160,'High, Advanced Off, 8xAF, FXAA','Нет','Нет',82.3,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','В 4K снижен пресет, сравнение с FHD/QHD неравнозначно.'),
(6,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',1920,1080,'Ultra','Нет','Не указано',73.2,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','PC-порт God of War (2018).'),
(6,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',2560,1440,'Ultra','Нет','Не указано',56.5,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','PC-порт God of War (2018).'),
(6,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',3840,2160,'Ultra','Нет','Не указано',34.8,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','PC-порт God of War (2018).'),
(10,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',1920,1080,'Ultra, AF High, TAA','Не указан','Не указано',71,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Исторический тест.'),
(10,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',2560,1440,'Ultra, AF High, TAA','Не указан','Не указано',49.6,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Исторический тест.'),
(10,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',3840,2160,'Ultra, AF High, TAA','Не указан','Не указано',22.4,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Исторический тест.'),
(11,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',1920,1080,'Ultra, TAA','Нет','Не указано',163,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Исторический тест.'),
(11,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',2560,1440,'Ultra, TAA','Нет','Не указано',115,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Исторический тест.'),
(11,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',3840,2160,'Ultra, TAA','Нет','Не указано',57,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Исторический тест.'),
(12,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',1920,1080,'Ultra + HD Textures, TAA','Нет','Не указано',123.6,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Без DXR.'),
(12,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',2560,1440,'Ultra + HD Textures, TAA','Нет','Не указано',82.7,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Без DXR.'),
(12,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',3840,2160,'Ultra + HD Textures, TAA','Нет','Не указано',25.1,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Без DXR.'),
(13,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',1920,1080,'Ultra, 16xAF','Нет','Не указано',83.5,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Не Enhanced Edition.'),
(13,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',2560,1440,'Ultra, 16xAF','Нет','Не указано',64.9,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Не Enhanced Edition.'),
(13,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',3840,2160,'Ultra, 16xAF','Нет','Не указано',38.1,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Не Enhanced Edition.'),
(14,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',1920,1080,'Badass, DX12','Не указан','Не указано',100,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Исторический тест.'),
(14,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',2560,1440,'Badass, DX12','Не указан','Не указано',65.8,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Исторический тест.'),
(14,9,'AMD Ryzen 9 7950X, 32GB DDR5-6000',3840,2160,'Badass, DX12','Не указан','Не указано',29.7,'Notebookcheck: XFX RX 7600',@rxreview,'2023-08-31','Исторический тест.');
UPDATE schema_info SET version=1;
COMMIT;
