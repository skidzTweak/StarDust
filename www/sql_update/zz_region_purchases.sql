DROP TABLE IF EXISTS `region_purchases`;
DROP TRIGGER IF EXISTS `trigger_region_purchases_default_date`;

CREATE TABLE `region_purchases` (
  `id` int NOT NULL AUTO_INCREMENT ,
  `user_agent_id` varchar(36) NOT NULL,
  `product_id` int not null,
  `name` varchar(36) NOT NULL,
  `notes` varchar(1024) NOT NULL,
  `shape_id` int not null,
  `dateof` date,
  `order_complete` int,
  `up` int,
  `purchase_id` varchar(36) NOT NULL,
  `paypal_raw` text not null,
  `complete_reference` varchar(36) not null,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1;

CREATE TRIGGER trigger_region_purchases_default_date
  BEFORE INSERT ON region_purchases
  FOR EACH ROW
    SET NEW.dateof = curdate();
