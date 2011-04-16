DROP TABLE IF EXISTS `region_products`;
CREATE TABLE `region_products` (
  `id` int NOT NULL AUTO_INCREMENT ,
  `name` varchar(128) NOT NULL,
  `sizeof` varchar(36) not null,
  `prims` int,
  `users` int,
  `description` varchar(1024),
  `price` float,
  `active` int,
  `button_id` varchar(60),
  PRIMARY KEY (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1;

INSERT INTO `region_products` (`name`, `sizeof`, `users`, `prims`, `description`, price, active, button_id) VALUES
('Tiny', '64x64', 3, 1000, 'A small place to call your own.', 7.95, 1, '2D72BYBQC5SYY'),
('Quarter Pounder', '128x128', 12, 4000, 'Quarter the size of a normal sim.', 39.99, 1, 'KPDALPETXHZCQ'),
('Full Size', '256x256', 36, 16000,'A Full size sim.', 79.99, 1, 'SBQ76QQZ9PQJA'),
('Super Sized', '512x512', 36, 16000, 'Four times the size of a normal sim, but with the same resources.', 99.99, 1, '54JANJADDKTLQ')
;