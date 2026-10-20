USE repairdesk;
START TRANSACTION;
INSERT INTO staff(id,name,login,password_hash,role) VALUES
(1,'Александр Волков','admin','210000:UmVwYWlyRGVza1NlZWQwMDE=:S+pqvCEuXgfYVgdJNZDdlxstVZxQDGgVyZVipfEwOL8=','Администратор'),
(2,'Мария Орлова','reception','210000:UmVwYWlyRGVza1NlZWQwMDE=:S+pqvCEuXgfYVgdJNZDdlxstVZxQDGgVyZVipfEwOL8=','Приемщик'),
(3,'Дмитрий Соколов','master','210000:UmVwYWlyRGVza1NlZWQwMDE=:S+pqvCEuXgfYVgdJNZDdlxstVZxQDGgVyZVipfEwOL8=','Мастер');
INSERT INTO clients(name,phone,email,notes) VALUES
('Артем Смирнов','+7 (900) 000-10-01','artem@example.test','Демонстрационные данные. Связаться перед заменой деталей.'),
('Екатерина Белова','+7 (900) 000-10-02','','Демонстрационные данные.'),
('Михаил Козлов','+7 (900) 000-10-03','mikhail@example.test','Демонстрационные данные. Забирает после 18:00.'),
('Анна Лебедева','+7 (900) 000-10-04','','Демонстрационные данные.'),
('Илья Морозов','+7 (900) 000-10-05','','Демонстрационные данные. Подбор игрового ПК.');
INSERT INTO parts(id,category,brand,name,price,stock,min_stock,socket,memory_type,wattage,capacity,source_url,notes) VALUES
(1,'Процессор','AMD','AMD Ryzen 5 5600',10500,4,1,'AM4','DDR4',88,0,'https://www.amd.com/en/products/processors/desktops/ryzen/5000-series/amd-ryzen-5-5600.html','6 ядер, 12 потоков. Ориентир питания PPT 88 Вт.'),
(2,'Процессор','AMD','AMD Ryzen 5 7600',18500,3,1,'AM5','DDR5',88,0,'https://www.amd.com/en/products/processors/desktops/ryzen/7000-series/amd-ryzen-5-7600.html','6 ядер, 12 потоков. Ориентир питания PPT 88 Вт.'),
(3,'Процессор','Intel','Intel Core i5-12400F',11900,3,1,'LGA1700','DDR4|DDR5',117,0,'https://www.intel.com/content/www/us/en/products/sku/134587/intel-core-i512400f-processor-18m-cache-up-to-4-40-ghz/specifications.html','6 ядер, 12 потоков. Максимальная турбомощность 117 Вт.');
INSERT INTO parts(id,category,brand,name,price,stock,min_stock,socket,memory_type,form_factor,max_memory_gb,cpu_support,source_url,notes) VALUES
(4,'Материнская плата','MSI','MSI B550M PRO-VDH WIFI',11500,3,1,'AM4','DDR4','Micro-ATX',128,'AMD Ryzen 5 5600','https://www.msi.com/Motherboard/B550M-PRO-VDH-WIFI/support#cpu','Ryzen 5 5600: проверить минимальный BIOS в CPU Support перед прошивкой.'),
(5,'Материнская плата','MSI','MSI PRO B650M-A WIFI',17900,2,1,'AM5','DDR5','Micro-ATX',128,'AMD Ryzen 5 7600','https://www.msi.com/Motherboard/PRO-B650M-A-WIFI/support#cpu','Проверить актуальную таблицу CPU Support и установленный BIOS.'),
(6,'Материнская плата','MSI','MSI PRO B660M-A DDR4',11900,2,1,'LGA1700','DDR4','Micro-ATX',128,'Intel Core i5-12400F','https://www.msi.com/Motherboard/PRO-B660M-A-DDR4/support#cpu','Только DDR4. Поддержку BIOS сверить на сайте MSI.');
INSERT INTO parts(id,category,brand,name,price,stock,min_stock,wattage,capacity,length_mm,power_connectors,source_url,notes) VALUES
(7,'Видеокарта','MSI','MSI GeForce RTX 4060 VENTUS 2X BLACK 8G OC',32900,4,1,115,8,199,'1×8-pin','https://www.msi.com/Graphics-Card/GeForce-RTX-4060-VENTUS-2X-BLACK-8G-OC/Specification','Настольная RTX 4060; тесты относятся к GPU. Частоты модели могут отличаться.'),
(8,'Видеокарта','MSI','MSI GeForce RTX 4070 VENTUS 2X 12G OC',62900,1,1,200,12,242,'1×8-pin','https://www.msi.com/Graphics-Card/GeForce-RTX-4070-VENTUS-2X-12G-OC/Specification','Настольная RTX 4070, 12 ГБ.'),
(9,'Видеокарта','Sapphire','Sapphire PULSE Radeon RX 7600 8GB',27900,0,1,165,8,240,'1×8-pin','https://www.sapphiretech.com/en/consumer/pulse-radeon-rx-7600-8g-gddr6','Ожидается поставка.');
INSERT INTO parts(id,category,brand,name,price,stock,min_stock,memory_type,capacity,source_url,notes) VALUES
(10,'Память','Kingston','Kingston FURY Beast DDR4 32GB 3200 (2×16)',6900,5,2,'DDR4',32,'https://www.kingston.com/en/memory/gaming/kingston-fury-beast-ddr4-memory','Комплект 2×16 ГБ. Частота XMP требует проверки QVL.'),
(11,'Память','Kingston','Kingston FURY Beast DDR5 32GB 5600 (2×16)',9500,3,1,'DDR5',32,'https://www.kingston.com/en/memory/gaming/kingston-fury-beast-ddr5-memory','Комплект 2×16 ГБ. Проверить QVL.'),
(12,'Накопитель','Kingston','Kingston NV2 1TB M.2 NVMe',5900,6,2,'',1000,'https://www.kingston.com/en/ssd/nv2-nvme-pcie-ssd','M.2 2280, PCIe 4.0. Совместимость слота проверить по руководству платы.'),
(13,'Накопитель','Samsung','Samsung 870 EVO 1TB SATA',9900,2,1,'',1000,'https://semiconductor.samsung.com/consumer-storage/internal-ssd/870evo/','2.5 дюйма, SATA. Нужны SATA-кабель и питание.');
INSERT INTO parts(id,category,brand,name,price,stock,min_stock,wattage,power_connectors,source_url,notes) VALUES
(14,'Блок питания','be quiet!','be quiet! System Power 10 650W',6900,4,1,650,'1×8-pin|2×8-pin','https://www.bequiet.com/en/powersupply/system-power-10/','PCIe 6+2-pin считается 8-pin. Проверить число кабелей ревизии.'),
(15,'Блок питания','be quiet!','be quiet! System Power 10 450W',4900,2,1,450,'1×8-pin','https://www.bequiet.com/en/powersupply/system-power-10/','Проверить разводку кабелей конкретной ревизии.');
INSERT INTO parts(id,category,brand,name,price,stock,min_stock,supported_forms,max_gpu_mm,source_url,notes) VALUES
(16,'Корпус','DeepCool','DeepCool CC560',5500,3,1,'ATX|Micro-ATX|Mini-ITX',370,'https://www.deepcool.com/products/Cases/CC560-Mid-Tower-ATX-PC-Case/2021/14424.shtml','Лимит GPU без фронтального радиатора. Учитывать высоту кулера.'),
(17,'Корпус','Cooler Master','Cooler Master MasterBox Q300L',5900,2,1,'Micro-ATX|Mini-ITX',360,'https://www.coolermaster.com/en-global/products/masterbox-q300l/','Проверить высоту кулера и длину блока питания.');
INSERT INTO services(name,price) VALUES
('Диагностика неисправности',900),('Чистка ПК и замена термопасты',1800),('Чистка ноутбука и замена термоинтерфейса',2500),('Установка накопителя',800),('Установка оперативной памяти',500),('Сборка и проверка ПК',3500),('Установка и настройка драйверов',1200),('Восстановление загрузки системы',1600),('Замена разъема питания',2800);
INSERT INTO orders(client_id,device_type,device,serial,complaint,device_condition,diagnosis,status,technician_id,created_at,due_at) VALUES
(1,'Ноутбук','Lenovo IdeaPad Gaming 3','DEMO-LNV-001','Выключается во время игры, шумит вентилятор.','Зарядное устройство в комплекте. Потертости на крышке.','Загрязнение системы охлаждения. Требуется чистка и замена термоинтерфейса.','В ремонте',3,DATE_SUB(NOW(),INTERVAL 2 DAY),DATE_ADD(CURDATE(),INTERVAL 1 DAY)),
(2,'ПК','Игровой ПК / Ryzen 5','DEMO-PC-002','Не включается после отключения электричества.','Системный блок без кабеля питания. Корпус без повреждений.','','Диагностика',3,DATE_SUB(NOW(),INTERVAL 1 DAY),DATE_ADD(CURDATE(),INTERVAL 2 DAY)),
(3,'Ноутбук','ASUS VivoBook 15','DEMO-ASUS-003','Медленно загружается, зависает при работе.','Ноутбук и блок питания. Царапина рядом с тачпадом.','Ошибки чтения накопителя. Рекомендована замена SSD после согласования.','Согласование',3,DATE_SUB(NOW(),INTERVAL 3 DAY),DATE_ADD(CURDATE(),INTERVAL 1 DAY)),
(4,'ПК','Рабочая станция / Core i5','DEMO-PC-004','Периодические перезагрузки.','Системный блок. Пломб нет.','','Принят',NULL,NOW(),DATE_ADD(CURDATE(),INTERVAL 3 DAY)),
(5,'ПК','Игровой ПК / GTX 1660','DEMO-PC-005','Профилактика перед продажей.','Системный блок, сетевой кабель.','Проведены чистка, замена термопасты и нагрузочная проверка.','Готов',3,DATE_SUB(NOW(),INTERVAL 4 DAY),CURDATE());
INSERT INTO order_lines(order_id,kind,name,quantity,unit_price) VALUES
(1,'Работа','Диагностика неисправности',1,900),(1,'Работа','Чистка ноутбука и замена термоинтерфейса',1,2500),(3,'Работа','Диагностика неисправности',1,900),(5,'Работа','Чистка ПК и замена термопасты',1,1800);
INSERT INTO order_events(order_id,staff_id,message) SELECT id,2,'Демонстрационный заказ. Техника принята.' FROM orders;
INSERT INTO stock_moves(part_id,staff_id,quantity,reason) SELECT id,1,stock,'Начальные демонстрационные остатки' FROM parts WHERE stock>0;
INSERT INTO games(name) VALUES ('The Witcher 3 v4'),('Dota 2'),('Counter-Strike 2'),('Cyberpunk 2077 2.2'),('GTA V Legacy'),('God of War (2018)'),('NBA 2K27'),('Black Myth: Wukong'),('Red Dead Redemption 2');
COMMIT;
