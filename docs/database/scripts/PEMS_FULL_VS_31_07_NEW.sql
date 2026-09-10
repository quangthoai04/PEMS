-- --------------------------------------------------------
-- Host:                         hayabusa.proxy.rlwy.net
-- Server version:               9.4.0 - MySQL Community Server - GPL
-- Server OS:                    Linux
-- HeidiSQL Version:             12.21.0.7344
-- --------------------------------------------------------

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET NAMES utf8 */;
/*!50503 SET NAMES utf8mb4 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;


-- Dumping database structure for pems_db
CREATE DATABASE IF NOT EXISTS `pems_db` /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci */ /*!80016 DEFAULT ENCRYPTION='N' */;
USE `pems_db`;

-- Dumping structure for table pems_db.account_email_confirmations
DROP TABLE IF EXISTS `account_email_confirmations`;
CREATE TABLE IF NOT EXISTS `account_email_confirmations` (
  `confirmation_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `user_id` bigint unsigned NOT NULL COMMENT 'Tài khoản đang chờ xác nhận email',
  `target_email` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Email được chứng minh quyền sở hữu (email chuẩn hoá tại thời điểm phát hành)',
  `token_hash` char(64) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'SHA-256 (hex) của token gốc — KHÔNG bao giờ lưu token gốc',
  `status` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING' COMMENT 'PENDING / CONFIRMED / EXPIRED / SUPERSEDED / CANCELLED',
  `expires_at` datetime NOT NULL,
  `resend_count` int NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `confirmed_at` datetime DEFAULT NULL,
  `cancelled_at` datetime DEFAULT NULL,
  PRIMARY KEY (`confirmation_id`),
  UNIQUE KEY `uq_account_email_confirmations_token_hash` (`token_hash`),
  KEY `idx_account_email_confirmations_user` (`user_id`),
  KEY `idx_account_email_confirmations_status_expiry` (`status`,`expires_at`),
  CONSTRAINT `fk_account_email_confirmations_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `chk_account_email_confirmations_status` CHECK ((`status` in (_utf8mb4'PENDING',_utf8mb4'CONFIRMED',_utf8mb4'EXPIRED',_utf8mb4'SUPERSEDED',_utf8mb4'CANCELLED'))),
  CONSTRAINT `chk_account_email_confirmations_target_email_not_blank` CHECK ((char_length(trim(`target_email`)) > 0))
) ENGINE=InnoDB AUTO_INCREMENT=9 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Bằng chứng sở hữu email một-lần cho tài khoản nội bộ mới (P0 #1).';

-- Dumping data for table pems_db.account_email_confirmations: ~8 rows (approximately)

-- Dumping structure for table pems_db.agenda_template_defaults
DROP TABLE IF EXISTS `agenda_template_defaults`;
CREATE TABLE IF NOT EXISTS `agenda_template_defaults` (
  `agenda_template_default_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `campus_id` bigint unsigned DEFAULT NULL,
  `campus_scope_key` varchar(36) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'GLOBAL',
  `visit_type` enum('CAMPUS_TOUR','MEETING','WORKSHOP','SIGNING_CEREMONY','EXCHANGE','OTHER') COLLATE utf8mb4_unicode_ci NOT NULL,
  `agenda_template_id` bigint unsigned NOT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`agenda_template_default_id`),
  UNIQUE KEY `uq_agenda_template_default_scope_type` (`campus_scope_key`,`visit_type`),
  KEY `idx_agenda_template_defaults_template` (`agenda_template_id`),
  KEY `idx_agenda_template_defaults_campus_type` (`campus_id`,`visit_type`),
  KEY `fk_agenda_template_defaults_created_by` (`created_by`),
  KEY `fk_agenda_template_defaults_updated_by` (`updated_by`),
  CONSTRAINT `fk_agenda_template_defaults_campus` FOREIGN KEY (`campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_agenda_template_defaults_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_agenda_template_defaults_template` FOREIGN KEY (`agenda_template_id`) REFERENCES `agenda_templates` (`agenda_template_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_agenda_template_defaults_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=9 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Default agenda template mapping by campus/global scope and visit type. campus_scope_key derived by trigger.';

-- Dumping data for table pems_db.agenda_template_defaults: ~8 rows (approximately)

-- Dumping structure for table pems_db.agenda_template_items
DROP TABLE IF EXISTS `agenda_template_items`;
CREATE TABLE IF NOT EXISTS `agenda_template_items` (
  `agenda_template_item_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `agenda_template_id` bigint unsigned NOT NULL,
  `display_order` int unsigned NOT NULL DEFAULT '0',
  `start_offset_minutes` int unsigned NOT NULL DEFAULT '0',
  `duration_minutes` int unsigned NOT NULL,
  `title` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` text COLLATE utf8mb4_unicode_ci,
  `location` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `responsible_role_label` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`agenda_template_item_id`),
  UNIQUE KEY `uq_agenda_template_items_order` (`agenda_template_id`,`display_order`),
  KEY `idx_agenda_template_items_template_offset` (`agenda_template_id`,`start_offset_minutes`),
  KEY `fk_agenda_template_items_created_by` (`created_by`),
  KEY `fk_agenda_template_items_updated_by` (`updated_by`),
  CONSTRAINT `fk_agenda_template_items_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_agenda_template_items_template` FOREIGN KEY (`agenda_template_id`) REFERENCES `agenda_templates` (`agenda_template_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_agenda_template_items_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `agenda_template_items_chk_1` CHECK ((`duration_minutes` > 0))
) ENGINE=InnoDB AUTO_INCREMENT=25 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Agenda template timeline items using relative offset from campus planned_start_at.';

-- Dumping data for table pems_db.agenda_template_items: ~24 rows (approximately)

-- Dumping structure for table pems_db.agenda_templates
DROP TABLE IF EXISTS `agenda_templates`;
CREATE TABLE IF NOT EXISTS `agenda_templates` (
  `agenda_template_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `campus_id` bigint unsigned DEFAULT NULL,
  `campus_scope_key` varchar(36) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'GLOBAL',
  `visit_type` enum('CAMPUS_TOUR','MEETING','WORKSHOP','SIGNING_CEREMONY','EXCHANGE','OTHER') COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` text COLLATE utf8mb4_unicode_ci,
  `status` enum('ACTIVE','INACTIVE') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  `deleted_at` datetime DEFAULT NULL,
  `deleted_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`agenda_template_id`),
  UNIQUE KEY `uq_agenda_template_scope_type_name` (`campus_scope_key`,`visit_type`,`name`),
  KEY `idx_agenda_templates_status` (`status`),
  KEY `idx_agenda_templates_scope_type_status` (`campus_scope_key`,`visit_type`,`status`),
  KEY `idx_agenda_templates_campus_type_status` (`campus_id`,`visit_type`,`status`),
  KEY `fk_agenda_templates_created_by` (`created_by`),
  KEY `fk_agenda_templates_updated_by` (`updated_by`),
  KEY `fk_agenda_templates_deleted_by` (`deleted_by`),
  CONSTRAINT `fk_agenda_templates_campus` FOREIGN KEY (`campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_agenda_templates_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_agenda_templates_deleted_by` FOREIGN KEY (`deleted_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_agenda_templates_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=9 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Agenda template header by campus/global scope and visit type. campus_scope_key derived by trigger.';

-- Dumping data for table pems_db.agenda_templates: ~8 rows (approximately)

-- Dumping structure for table pems_db.api_configuration_headers
DROP TABLE IF EXISTS `api_configuration_headers`;
CREATE TABLE IF NOT EXISTS `api_configuration_headers` (
  `api_configuration_header_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `api_config_id` bigint unsigned NOT NULL,
  `header_name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `header_value_encrypted` varchar(1000) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `is_secret` tinyint(1) NOT NULL DEFAULT '1',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`api_configuration_header_id`),
  UNIQUE KEY `uq_api_header_name` (`api_config_id`,`header_name`),
  KEY `idx_api_headers_config` (`api_config_id`),
  CONSTRAINT `fk_api_headers_config` FOREIGN KEY (`api_config_id`) REFERENCES `api_configurations` (`api_config_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Explicit API request headers; replaces api_configurations.headers_json.';

-- Dumping data for table pems_db.api_configuration_headers: ~0 rows (approximately)

-- Dumping structure for table pems_db.api_configurations
DROP TABLE IF EXISTS `api_configurations`;
CREATE TABLE IF NOT EXISTS `api_configurations` (
  `api_config_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `api_code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `provider_name` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `purpose` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `base_url` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
  `default_method` enum('GET','POST','PUT','PATCH','DELETE') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'POST',
  `auth_type` enum('NONE','API_KEY','BEARER_TOKEN','BASIC','OAUTH2','CUSTOM') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'NONE',
  `api_key_encrypted` varchar(700) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `bearer_token_encrypted` varchar(700) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `basic_username` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `basic_password_encrypted` varchar(700) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `oauth_client_id` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `oauth_client_secret_encrypted` varchar(700) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `oauth_token_url` varchar(700) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `oauth_scope` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `body_template_text` longtext COLLATE utf8mb4_unicode_ci,
  `settings_json` json DEFAULT NULL COMMENT 'Non-secret provider settings such as project_id, location, processor_id, endpoint',
  `credentials_json_encrypted` longtext COLLATE utf8mb4_unicode_ci COMMENT 'Encrypted credential payload; never expose raw value',
  `secret_ref` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Reference to server env/secret manager when credentials are not stored in DB',
  `data_sensitivity` enum('PUBLIC','INTERNAL','CONFIDENTIAL','RESTRICTED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'CONFIDENTIAL',
  `allows_provider_training` tinyint(1) NOT NULL DEFAULT '0',
  `retention_days` int unsigned DEFAULT NULL COMMENT 'How long raw OCR text/draft should be retained before purge',
  `rate_limit_per_minute` int unsigned DEFAULT NULL,
  `monthly_quota` int unsigned DEFAULT NULL,
  `retry_enabled` tinyint(1) NOT NULL DEFAULT '0',
  `max_retries` int unsigned NOT NULL DEFAULT '0',
  `cache_ttl_seconds` int unsigned DEFAULT NULL,
  `last_test_status` enum('SUCCESS','FAILED') COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `last_tested_at` datetime DEFAULT NULL,
  `last_test_message` text COLLATE utf8mb4_unicode_ci,
  `timeout_seconds` int unsigned NOT NULL DEFAULT '30',
  `status` enum('ACTIVE','INACTIVE','DISABLED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  `deleted_at` datetime DEFAULT NULL,
  `deleted_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`api_config_id`),
  UNIQUE KEY `uq_api_config_code` (`api_code`),
  KEY `idx_api_config_status` (`status`),
  KEY `idx_api_config_test_status` (`last_test_status`,`last_tested_at`),
  KEY `idx_api_provider_status` (`provider_name`,`status`)
) ENGINE=InnoDB AUTO_INCREMENT=21011 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='API config + encrypted credentials JSON';

-- Dumping data for table pems_db.api_configurations: ~5 rows (approximately)
INSERT IGNORE INTO `api_configurations` (`api_config_id`, `api_code`, `name`, `provider_name`, `purpose`, `base_url`, `default_method`, `auth_type`, `api_key_encrypted`, `bearer_token_encrypted`, `basic_username`, `basic_password_encrypted`, `oauth_client_id`, `oauth_client_secret_encrypted`, `oauth_token_url`, `oauth_scope`, `body_template_text`, `settings_json`, `credentials_json_encrypted`, `secret_ref`, `data_sensitivity`, `allows_provider_training`, `retention_days`, `rate_limit_per_minute`, `monthly_quota`, `retry_enabled`, `max_retries`, `cache_ttl_seconds`, `last_test_status`, `last_tested_at`, `last_test_message`, `timeout_seconds`, `status`, `created_at`, `created_by`, `updated_at`, `updated_by`, `deleted_at`, `deleted_by`) VALUES
	(1, 'GOOGLE_DRIVE_STORAGE', 'Google Drive Storage', 'Google Drive', 'GOOGLE_DRIVE_STORAGE', 'https://www.googleapis.com/drive/v3', 'POST', 'OAUTH2', NULL, NULL, NULL, NULL, NULL, NULL, 'https://oauth2.googleapis.com/token', 'https://www.googleapis.com/auth/drive', NULL, NULL, 'e4+UYKU0IQU0evkzczc6go4OnDOm+xJjrKAwfRy4eSxbd9ugJQ2HOx9Lu8TJ8lvo4YmMIZYVs3VrLDgqCIdWvOu9/VlGtwKJypgrIg/e3+ZFvsWAjhPpW4CKhkVSEmuMkadid2YDbVkX5lhTEuF+iXk51IwI0nx3hy/64wpu9nEIDx+eadR4cncyJqHu8XCflyTron5F', NULL, 'CONFIDENTIAL', 0, NULL, NULL, NULL, 0, 0, NULL, 'SUCCESS', '2026-08-26 11:54:06', 'Kết nối Google Drive thành công. Đọc được thư mục gốc "PEMS_STORAGE".', 30, 'ACTIVE', '2026-03-01 09:00:00', 1, '2026-08-26 11:54:06', 1, NULL, NULL),
	(2, 'BUSINESS_CARD_OCR_GOOGLE_DOCUMENT_AI', 'Google Document AI - Business Card OCR', 'GOOGLE_DOCUMENT_AI', 'BUSINESS_CARD_OCR', 'https://asia-southeast1-documentai.googleapis.com', 'POST', 'CUSTOM', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '{"endpoint": "asia-southeast1-documentai.googleapis.com", "location": "asia-southeast1", "project_id": "pems-production", "processor_id": "9f4642de7b8f8b25", "max_file_size_mb": 10, "allowed_mime_types": ["image/jpeg", "image/png", "image/webp", "application/pdf"]}', 'rCiKjYYKdkIpuzh45Txzuc4Ozjbwg1GYvM7+KLSsppY5ING+EUAPh8JN/S2E67aTkjnVE0gZ9HvNG0qt+BNDOc2YvZf3zFv811VdCE6iIo6oBQnvAoHFz10rxg39r68WpxzSaEWYIGY27aaG/alLKmB+RIjByg82mW2qC0qqjWXNP/Dv11SSbLPJWVw5/Lqkmxa+Bq/1brsn2Fo6TWNn9MuUldk1B4XmNGYv/o5Ou+vdDeLZvICW2xswYRtiMLWJJW9kbd618WQ/SFcC2o0lbFgTPC2Js78apwgjSDXXvybqIABaMgVrX0trbkcRD0Fn02Pn+HBX0i3osCF50E4G3xmA+jNmOFlOhSJCw1K9QHsXQhmD0SKEj41aX2mqBP7HJw8dMROMms7ucB2TivfxU5xrjUKoidBiEmoMhbHp6i4wY9Sc+YiK5+o3A3kgLLcUpdzRda+BmyPoRwt3ChJFMVOkbu5Tmzx5IWJS65aS90Syac4HkcmThKKmhqRMGykdBVLz7TRGT/C8OC3+LeqTEKc1c/GCaabel//VhCQcwcNdGm82wS0rHYRWUnPYb5/LiO8zh13jMFYAjBHnZLuyfjO8c4EP0yWrNTy1PrdQAg42Y8L2dJ3ZOrdvIQtLCyRm4uGsE1aPW2Sx6Oi65rObMvOGhvv7HWWVftFjHp0CCsjs10mjNzaEtWJFQ2h9Ph2G7u0FEBUS5lZmx+iGrqU3+SAsMbcFt/BUEofhrSa/DdtL6dtpTPytvl97TCBv3fk6EyctaWsdvQUs4F/GaXRYKBcmgGh19XVdIrA1pS57sb4NSy3EOwAGINEgril71OW4UsOzbBTwTbw/8HoogyRUC6zrOBsMI4kgBBfJzoqW8goqsEAW0KuiEYgO7cxpwM/3hkkrnQdzEBpuQ2B8k9/Tl2xHAPRuFmYxfYC0KiSNHX2E0VbWw+bxWE92UhpxBwnNzgfHaE9NFfOke4b6hBRqYLtcYqFpXRVAbqxPlXqNoY/r6Hx2fD0IeBmmv2tsgkIFdHStJTq7dCOqT7MRdxB/gcJ2+xftbC7leElU6XSsK61+zu/SWPQ0ayEAYXHuF69RFOb4qXpdybWI+Vrq/UwsvdYgRU6K6Jk7hvYyw7PkvcaipLx+BSGqwBnJLdgCzUfDf78YwsuOu4jB0pKn0p1th5XlJ2iXql00wTevaCDxKVipk36ipD3nD9U8eWOWJb/9REDY0rdcIp3ZfiqfeIvc9d2BUFZvKEKaOQjZEFyc2aK2WncaKVwecydmyqPiK/YkME6MMeJw0zynDdlTfXdgP1Ef11r4ht4pRxaPY5+ZkKX7OBzEYEnpIgv+AErPp9NYlEiqXRFXfY6gf2OnkF75FoQO+ifPWgZK2gYC+pE2gGnupDlulEEfhnoKWXHQDvH7q4RM3yfaZrqw7VtgfScAam+V5YaNO80rotkQL3eoDrMgZ9mHqO7/tRfTbFOaP3qRpatOx1n6qARyQPOxtsUmRwbobhDAmAhAfeH9Z99HYGnxEkg8yb0oQ0AUjh+mXfuTvn6oSCXoybgFZyfHyUMACozIolBvfgfeNR1l68zXtY25XptjLFLknr4YcdOuB7ymGYaA8OpHeAhMXK5/StRcRgQPLWvxB92rBuPJrOPTqD/02Z+oKIvigdWOE1hP8weQwyyCbVsd6lF7sVh7IlXToREM5aeZeUwlTg7AxUACXLR4+ZHSGDERMIzM82dj6NQQeqZG5uEgX4a9yvNSYtscjmZX6m/aXFZvIELXp4RDf0F2WTMwcbixkPwpZJLBCmIcpc5FxoX987DEyj1CsCenRcrygERKNi4Zro/QsagjG6aKF2K0/2lygB7TLfRN/a3VxyinS//X5mF1Q2OHNNNL5cSpUPMCucPd5xeVMWGYQqn3OpLyaeAE1UXuI4ZXG4T5b0SHOcbFNGFwx7hLF+s2bR0IFla/TokpxbWxAtJkJTcX8BM0hrN08T/fXO06vF8HpC7XyCGUki9B3QqSQjAv9Hz62Z92uZvSo5A7uayU3sgNsISjgNWCP0v3KqeqTQFfa/1390wgDVEonXn6ZBE3cmDAMRk3dQqdpVLOyKPepvaALeFgSu6onyEXm6KMp7RweyDaJOupV4luv0GtOMm2tseyC6pl5SSky2tcGYSQCE9qNl7tel8kLjzS5+vutk4oKaM3tuhSHLxUL8vPU050Mn/eoV2g0rnDj+zzQp2r2CKZn7DnLt/SsIp/sh8svlM7U8ZoIGBjR9LEE0vIkI9sOhNML3UioCd8jmpjIe5L2mqRUvUrFi7Jv4c3FxAT0oPna1i9ZPTKQbhjtvLBtk7PHQlxY0We81GS9JkwSSgHk0n8mQPNMaUQGn1t2ZtdRNHRua06KmOKaBaFXDLYQ+LvF9PpxVTwZymA8v+QcsXsQhMXeD0ggp0+JehmorjF3bLRj43s1Al55BtRNlOJWjSaIEytz6j722m664iaSzfCqDdNsQ1BuPlGBlnIrf605URDzbdHhePk3PsSj6xcRlq4Is+w5c+O2cZd7IkBnwp66CTxFCH9n8rJPv69BP0+F7GX+38H1hfeBc+SB1Va7pgIEDVR6ta0WHxnprcrzTNlts45mYwjlX+Za4fdz+Onwl6y8kW7MiWrnuFg9FkHcTvXUI38yEMJzVBXFNBMgAZSy3OPHC73MGQvwQZtvIN07skALnVkJfVAO11BLng3QbFM6QEkgVqWDgFzRESR0ynLUHAsMHR6QM4cYuovTRVFAkYqnhXkgTxJBh3udMLTdjUOaRFukxQG/1z6CfnRvio8Oe3s1x4SpT9+Y9hZmqg5CY1aSEn3k5xnYpiwS/DFB8DA10Tj6ws2OGbCvDYTaczqkxTxuJdF78qbiBBUT8FefRkQHYebWfCHxWB9r7KK8A10L0sDQt6WVbn/R+iA054kTVusUZYaAzb3dPg5XZKmZfXG7ChQHJVp0efEwiyxgEn4CBX778JTTK5aGCeowm199iX+Y8UM46HNG/9cUcFo0Gzdrfacn4DnrrRQVce4V8f8AgFmfeX4TNT7TZxYJnimLmZIjPUtOTokyrn35BjYEDpmyjFG8d/w6rS8S3QYtCrx8Xo0jkO2oigxIX+YLeofDBzVrJ9UB/Pt7M5C8EUZPe6h1yU7R/lATpTJen9/eV/g/p9N+0yqz8/epVwAL+NZjEUHuOcfU3PYtZ+c/ASvehw=', 'GOOGLE_DOCUMENT_AI_SERVICE_ACCOUNT', 'CONFIDENTIAL', 0, 30, 20, 1000, 1, 2, NULL, 'SUCCESS', '2026-08-26 11:54:10', 'Kết nối thành công. Processor: pems-business-card-ocr (ENABLED).', 60, 'ACTIVE', '2026-08-08 17:04:55', 1, '2026-08-26 11:54:10', 1, NULL, NULL),
	(21008, 'NEWS_TRANSLATION_GOOGLE_CLOUD', 'Google Cloud Translation - News & Content', 'GOOGLE_CLOUD_TRANSLATION', 'NEWS_TRANSLATION', 'https://translation.googleapis.com', 'POST', 'CUSTOM', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '{"endpoint": "translation.googleapis.com", "location": "global", "project_id": "pems-production", "processor_id": "", "max_file_size_mb": 10, "allowed_mime_types": ["image/jpeg", "image/png", "image/webp", "application/pdf"]}', 'hmey32LuezPViIfMLWqxOw27SIKcuCb/bNFH0ldNQy2RliyGwkJmonJ12Lj5R1QbHZTasXDGessPWgGdVTfL1Cx32fbicrcfPcgalpNH5b+iiVWxvxkePncLLrZFMsOkyw7VwM+A60TylwOOLwRb5d94xEPnXDfXGaIBKJspX5PvjuO0mwzZNY7SycZBUhUsFrZnk4470+tVtZNZlyrp2HET8cEyUQEkIg9hdhtF/Nu667RYPUDQUoQ1/XwPPVyzeNr2fwoe6bAS8tLlNYN7gp6ZJaHpr+nu/wFH5JaDjQRpi2vY+ONuPYNvVEvYjbFRrbfRC0WehFUHz5azFi0pbgMWw7AQ3EissHND1sg+hHD9UZdHxaeqYLHPNDS2ezFqyMA/3c9mDrK5qmJcVkdwGQa4aGPyeKjwdNmjGFexLtrtXFxPmSN19tYpGTryc/722BMQoodJG/ewIWMy2vFkKqlQYuB8/LjZGOYUWLIiYYX87KYwqPX+VQh08mzy3vSQRRRP3xNrAVJ/G2jf0U/cgvVAHGQs/41dZ57XTUDtI8xju2lfl/3w/DD5n4u7r/Dz4tDVjVgqZUmS9yo9RwXBRoDOY/rnXfvJ4ZK4THelPD1I6yuemb2+LbyUBfdc0IR9em/degc1f1AK+YC1rnBmAPGEueREx47pmfyFrNbxRyydvp/QSluBFHgQcMN8s54pcNDmYpOurAeZ16DE4XjcZE0ly5oZ9DSHFUkdNkTvPEz0eiKHl+jEWZaO8Nz/R7QjufTN7vlq8F+dEQLtj/GPcgy6+BRaQtKb6+trMt0ms8UK5VdG2NZ3m5mixQUVDA/Aqx4v75BqA/B9ASrbfoVAmTjEEc986ucABz9sEuSPxbPOc8WOX64kO5EXxqiRjEiT1nKzcAvWeJCpxZwR1w6y6MdJqnPxJyHZd0G7uOzvWfdHbLP+0lPNMgzNhLevf5bwpTHUPvhsog04lUmB9pdXm+7QnYrquo5qE3FGJW3vucTIhrWzBI+b/9xKW1JC7yaUG2MhSJa+mi+9hhMElymDRtaBpC6PmTBG5LFQVd1X118iS9OznU4sWCaKwn5wI+zGCr57ror5zzjx4qh4J1141V/EzwDZSKSPCc2AARE1k6R0Zx3U/BM++poI57YZMRI4jWj1sHGYtBCQirY6InE+6rP2Rsui1+iGq1QUGH92vEqX/cJMD60Ag3j3bJBvDkAPb7RpiVZsKuXU+aqWG1eUH1bsvS83i/gRYpUPIlybX5kfGpiNOTbl0vAZ30qa/TziHEYhdX/plR57lgnuzvCACkEqrnGEwNc5QK6dDHO1C1t4AFCWpSACiGLfqz9NqH/xz7bEqjloNkOMupUc36opzo5SR5eTLtvTDQd4rebe4brTSEdj34ou6+4j6nUJ9LNAgw4HhtYfTkb4YEJ9k1ikoYkW6m5VvlhZFjbPAXfUKJAdVXQYZQT5wVt47ET1F6nWJH2Om+o30dYSZ0Cx6dJR9A7gAP95ivdjGaJG884nTzTkYLyZvLfonVADYWLb1X639PcC5DiqCGq2CY9jtSyUuIiTRufA38HjBA0mTR9izXx9D5YdRp9ZZO27QToemKOlLLNvR/TgCs6jMt+2Koubjj+eWsXzh2/x6fFtIA4Ko2JL00A3TAqU3/t62ma6ikg7JKU/23O9GxsB3jB5c3SWRLcOJ8U5QCekyJm/wbRi0XmT3ElkwkmNQjAA066xqfxOyKUdsfw+I0lM9ONc/xOIVCswTsoW0KJ/MFuR17qcpOoZ9jydAe8v8i6ZcrOUsouuc7EW8FLrZzzaErkxtssQB+1t0hWN2rQ0Dkoy0QnivG5Yb0vsDu4l8a5tusEJRaiDFl8QLmUmQWVUESPsyYpiFx5O8bgp19Xs/OvFlBSIC324s83zbW9eGLfoMMYJbcfNhXdC/LdGhIvpVBTFwFAYLEVfoL9H2o768NlVs9AhAbHOs2mpmPE3IpE8HWNaKmFrKxCoumvfkyd2kDYwehQ5Qk3pnR5igyxkot30BtLR226F0xEgb3IjvOq+GfXKzgCgMxcvlC5HqT36ts0Xv2RHRsU2out6uXysQi3o4bOFbFQmvTo9T792Mc1HLSkdBFKtEOdGlynSUKVqGzYMjmKJD6oqxMXOOIMNhoxbqlw4sYh/VoNFYW6UzKaWApisU34ItRbXR7jZNoeoFvsPknfzlY95wiCJsZwyz1czol1utLFA5kQySLWVt4cl5N4GcIeHj0HL2/+UqvbG1Ci6tWuwP4xu8BTOKnYBNaO5fU6uhO6oywxxusp55HZtuoYNTRMkoL2p+RtXa0bldqtx2lQhB37n+IAv72wvusdjVSSvm4CHukyYItEH+2H708hrA4E6wzFFR2D7l2XubScuF4pm+Suh44c8woRdnIrtaqKqI3vAgn7jDFFnmKN3XKvrlRr3eqwcf7HYIraXIW7uQNAj09kDxywcXFuyK5NqhS4JxnAh66x2YzzRbnSDqvHGl1BKQH2sIGl/RRXhRIcIFsZiCQ9Vj7aG7K16hQyOaSxiOMadMqQbguhq980IDimv8HDhL21/z+pjILoKAF/2TS0jHPKc38q7Zsumvx5FxwaRiFiSeWidRlwpWUifJFT0GXqqLXJhGr2AmQ6bmwH1IgALoAGFU5KgsJoET039Q2mjRm4zzl+t6tfb828Maw2hQOHRQdC4t8RwzS4+e4fBvHSQuh8yTpGk+ng0LC6aKSNokkZl5VQyi2oRKakNZ/OFLVtbwNWNZT+BIllkO1q5hA2sc9EpcLc9YGShn4NtcBhC7WQ204Qqf7jAWQgFSAPCLqadv0cRQ2G78NHkwHzIQbQTaaZ6Cp9C04LF9W7DwAMqfkOGuuTuwO2jKFCAbWs4fiv3041Tn0EnvSbbzBx4WFnjb2yrWfxTTc37PpS/ZgYW8t4Xqu9RP6QSZZlUQqD78oGGmjFubnKRUC8sunVswJB4ajmleDro++Iqg3dFjsP6UK90hPmslTikgFoL2mE53ovvBKpBQAbErSTdvM1qwbEB9lzqlwD7afMMe3S8MAB9jrMvYtxBFjkUkdPE7EQrxZ4gBRQLyk/PyPCt097EixIS1eBLLQI/Ht3AeKocdmuxcr4CYxW9NbSp/U1oTlptWTTM1n3lQkYz0Hft5gzqsze09l6NIj+uO+JL39wcYCLjoB6MsoYDzTYNmliihdo9gjM=', 'GOOGLE_TRANSLATION_SERVICE_ACCOUNT', 'INTERNAL', 0, NULL, 60, 10000, 0, 0, NULL, 'SUCCESS', '2026-08-26 11:54:15', 'Kết nối Google Cloud Translation thành công ("Xin chào" → "Hello").', 30, 'ACTIVE', '2026-08-08 17:05:39', 1, '2026-08-26 11:54:15', 1, NULL, NULL),
	(21009, 'FACE_DETECTION_GOOGLE_VISION', 'Google Cloud Vision - Face Detection', 'GOOGLE_CLOUD_VISION', 'FACE_DETECTION', 'https://vision.googleapis.com', 'POST', 'CUSTOM', NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, '{"endpoint": "vision.googleapis.com", "location": "us", "project_id": "pems-production", "feature_type": "FACE_DETECTION", "max_file_size_mb": 8, "allowed_mime_types": ["image/jpeg", "image/png", "image/webp"], "max_faces_per_image": 50}', '+kCsnNmbiMpmPi6ux0Rni5h20f65bMfe4v+3JhFktEsiHCs1P1qaAwGTMECHGmdWRC0HdZtBx0x/41ydgMBfe1kGqJsHVazEL0dIl9rEnwMGuKG5H3RfM77v3oTTN5DM6isuLwDTwXAKCuv7YLHeV782acRpz8tHKXR3ici6zisnOJHOTIlnG29eCf4N6UCe0aNZ1+6kD4tGXkndyGAujwRyd30uPredkv4Gnpy1RBR14h2lN60x9jEuSJXO16eCg1tSQilmhqKOFIfNw/vGg+N3vFyNzjiWx4d5VWT50x+7GKkO49q/WKPkzEUAVZuFm0qF3QoYANYK9t3dWvseIAnDpbTro2HyJ9u0VT2JTHxVaZFYni/EwqB+as49rTKd4ZCulGLqWIlUJbq16MPdWYmjNiS/BjAEuMoUaeT1wxj2Co+JSB/XuBc3KvYMknpczXAU3QUNArAMYba+t3QATctli1ZRSsl1QRTZaIZDm7/pyeg+poWkNP9qPmfsfvpzdASvkhJT/jwb3Qy0XOyqpPq+DXpy/SWFpBNy1kDZ/5t0zStou39O/7SeCfao5zDw7GhBD/fspzh301TRfb9Gbgc563cEUSh3Kc64C2XTf0rvBDm4VHtxW8lD8hlukYj999IPGQDxT0Zi8ZTOZcw8wleDfkXbgM1x5r1JQjKbwB+6lOq6/5kotxsvsc5GLbpsMtTOvfLD4kC4E9NV8B8RQ+J93uShkiaR70GmSYMHgx3JYhnEQ8N+bEVW+9gdV4Tq54hJ1vhQnZuI6l+jQ2Cce1VRyUr71RZy4TafcrDcJ22iaz14Px6NuSk3XLb1MojnncOdCWwrfSxmm1dloQnDL+Kzo5eA/ipL/FT34lyGlmLDCZQeAxPNVMMwvWuZ6OvWpA6t2flwOL37t6kbf0gioQ1ZdI4UwBfw+yjyAW6J2n5Z4lhDnCklykIMUGrYqEDuZNKqbiygP82D4xw3MqWMhZscj83QDvZksWpeggSSmZzCnbHJx2gNeLzAJdlYQc7TpFUAaKhqsJO10aQ5LKlPT6m/nEKHxrjcRx69JIijLspiO7aPNt6rtxRIDu3TFtct1fwKcOe8cI4vSBrqYKdsNkNwYYZXoMnIOgK9j4JEZXb7rcWTcmgHSVfJ2bdnneOqAdzUAvq1fsVg0l5E2LOMc8hRdldft27ndaIDaZizwG/49hXvRXJLAjO1YcOLkmNZ5LXaLXdzzjUz73Njtckpj2as0zgtqSmCm/iWnbHZi14ipNj7SC7c45j8KJKqayc9eiFkFaojwAKFTXAW1sONP3X1eTk+SJKjqhTvmVuSxBOqeWuQ0ETRgaScBpu9DQz4Upc8bEt4CgepB/FnnJclWVC35AwTu97i0fBCUi9gVyb133R3RFw2oOIQyhbIjSFs1fPu6jniiN2FtsIHF08ZyND+DSETEHBF6Ya/ewIOEOdu5i9GbDPgngi5WqCrdqrRIdpYZh/HzFQGIObfbdc0gPJr++bYmmrJ681yNlBcS8YtML4Fany21sfxrtIh5fa4gdwU1jILAFxmd4v31bWjVRBps8hSRY9kHb6plqi6pmmPo5FEXVSMK8K04CNedtk76B+ega8a7CKFTLF6AXDJcStOkD4S4lJ5IcIxHrVmt2ZP7X6xlH5+ttxbczrA/kJ4S3BZTj/rm6EAXPQ3isMUNmv/EFlvi5C7OKtozxjPPvPQMuFAAWWvSZDiBhtnGB5HKpyctWKPYxJjGWs0WBLcH4uBAaRCjVxQzSkMok50/u621aAfFrLFJml/iOLCfp0m4s7fZJetGqGHpNgev+ruPKWtc/5oABa3xQ/vKLYA4TM5B7kcFuui/OyJa6Y1NFxySQPFQB0EKOAKbwOPSzJFBYBF6f5X2onZw+/SngULiNbhKrtBfE7w/uw1L9F0N9rNm5/UXLezYtsI8QzuFxaOohNHaYGk9DhS6GPubdYnOAXM89WlDf62HOUXpnvpL6tn+yogNUu7TD6lFv4GR0nyJ1PDovhRk1C8HakfOubLdDRVs08QRj1ZM0l1axH4aGCSN36eE62E1V/1kcgCtxGO2s9IaDZBmeaJafmQMSNiPuVlITaOYPgMwTcLpo31iHZkRuTusEOKv8f2FJTJCX49HzDyHRUWsQ9kl1ynMTIsZc5cToDxF9HhTUqG94DiykntrBQATndRE7kbWs0PpGswDhibPMSkzbroy9tPGv4W7c2PSLz1ln+lM0s4Kgsn5m1aRxlkTpX0c7yNscYGb1R/O1zbNq7XkMnIr9ye0owK/ATDZg3qIaQDUdm1HO1dd6wQWUSQoOj6TxlvynKcqG9cPZfgPhI4gvdU2HbJuUKxVEubFJ8NDXai26JnwqGEwRHCWxIqS9xh9pwqMc9gidLMNHx7UEcgELihI1VRq2uDHi0+l4G3plAW+FW8wW/lbWb8LT5qyM/QhcqOYSWlLW9L+hvUUC7aCTJ3g1P8Vul2TkhSreFv1SPAoFWuwFA4d11wz/pPnnyrm+9OotATCiBlExUcKnTIQdlMGLdSyN5oJhi87DSY+l1B1vdZddhNpesAPwL1+yB9cmsMXGsqXdF1xLWQrJ6Po8apcTx2JVujqdLAx1BKCqdZCCqiQGpVjZaakzrNhjjHQqTG6NmU6qP0IJPa9k5QNFRFUb0wxb2EEd52TjuSdeSY7WGnQZMLKKtLjCsI9/c9NpQ9Yt7e4kmf6moZ1HF1Ci42DhVXMsQ6CvY7BFqJi2kxn1FNYzM0wPJFT0/FxzB/tKAJq+A8CQYCTVVBgbSZwH0nAEU15tUuw4QFyPE9HBNe6w2dYiIarocKC3ZxT5V1YOoQDdm2Jn4Vsl5fCnKdNx+Frfh/eeVOLVouSlKS9g+jF+ASp87Vau8ZrqF4iHWeTrX9sEgDohrV+M5WSxZALoAzd5IJ1ArakE3F9xhafEy94FnWq+yUfy0xhIsUZJAVKMG4713cHUqr4GnNR+QT1tmhuPxQlBVHLz9NBeAOXIH8lWcvFAgRs95/2ksbCwzVdPevI+xWQTI0s0eHidye09wAiBg02Zr3MnSg4cG1jk4qplNYlWPgbH5yf5LHLUJDbhu3X4ZSKLuK9M/u7wdip6B/L6MDzCg+HCEO8UuUZFMIXml/Ip93MqkyylGeP9kTlvUz5AyRg/Q6uvLCuir0uaU/zxyJ2NrpIdVhFraMxlVu5aB1ZbLK9SY=', 'GOOGLE_VISION_SERVICE_ACCOUNT', 'RESTRICTED', 0, 30, 20, 1000, 1, 2, NULL, 'SUCCESS', '2026-08-26 11:54:18', 'Kết nối thành công tới Google Cloud Vision (FACE_DETECTION).', 30, 'ACTIVE', '2026-08-08 17:05:40', 1, '2026-08-26 11:54:18', 1, NULL, NULL),
	(21010, 'RESEND_EMAIL_DELIVERY', 'Resend - Gửi email hệ thống', 'Resend', 'EMAIL_DELIVERY', 'https://api.resend.com', 'POST', 'BEARER_TOKEN', NULL, 'Ch5gK+3YqETNl+PLvEhuYRfyy1dW+wxLIiIr1ulhy7zHnlPNf3U3Qhsn/NH0C9eOelBB1Ym7reB5t94UFyyDDA==', NULL, NULL, NULL, NULL, NULL, NULL, NULL, '{"from_name": "PEMS System", "from_email": "no-reply@mail.pems-fpt.site", "reply_to_name": "Ban quản trị PEMS", "reply_to_email": "managementsystemvolunteer@gmail.com"}', NULL, NULL, 'CONFIDENTIAL', 0, NULL, 60, 10000, 0, 0, NULL, 'SUCCESS', '2026-08-26 11:54:21', 'Kết nối Resend thành công. Email test đã gửi tới admin@fpt.edu.vn (ID: ee4e710f-55ca-4fb3-b617-8622a3a10204).', 30, 'ACTIVE', '2026-08-08 17:06:56', NULL, '2026-08-26 11:54:21', 1, NULL, NULL);

-- Dumping structure for table pems_db.api_request_logs
DROP TABLE IF EXISTS `api_request_logs`;
CREATE TABLE IF NOT EXISTS `api_request_logs` (
  `api_request_log_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `api_config_id` bigint unsigned NOT NULL,
  `campus_id` bigint unsigned DEFAULT NULL,
  `requested_by` bigint unsigned DEFAULT NULL,
  `related_type` varchar(80) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `related_id` bigint unsigned DEFAULT NULL,
  `endpoint` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
  `method` enum('GET','POST','PUT','PATCH','DELETE') COLLATE utf8mb4_unicode_ci NOT NULL,
  `http_status` int DEFAULT NULL,
  `response_time_ms` int unsigned DEFAULT NULL,
  `request_size_bytes` bigint unsigned DEFAULT NULL,
  `response_size_bytes` bigint unsigned DEFAULT NULL,
  `success` tinyint(1) NOT NULL DEFAULT '0',
  `error_code` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `error_message` text COLLATE utf8mb4_unicode_ci,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`api_request_log_id`),
  KEY `idx_api_logs_config_time` (`api_config_id`,`created_at`),
  KEY `idx_api_logs_campus_time` (`campus_id`,`created_at`),
  KEY `idx_api_logs_user_time` (`requested_by`,`created_at`),
  KEY `idx_api_logs_success_time` (`success`,`created_at`),
  KEY `idx_api_logs_related` (`related_type`,`related_id`),
  CONSTRAINT `fk_api_logs_campus` FOREIGN KEY (`campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_api_logs_config` FOREIGN KEY (`api_config_id`) REFERENCES `api_configurations` (`api_config_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_api_logs_user` FOREIGN KEY (`requested_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=46 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='External API request logs. Never log full secret/token.';

-- Dumping data for table pems_db.api_request_logs: ~45 rows (approximately)

-- Dumping structure for table pems_db.api_usage_quotas
DROP TABLE IF EXISTS `api_usage_quotas`;
CREATE TABLE IF NOT EXISTS `api_usage_quotas` (
  `api_usage_quota_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `api_config_id` bigint unsigned NOT NULL,
  `campus_id` bigint unsigned DEFAULT NULL COMMENT 'NULL = global quota',
  `campus_scope_key` varchar(36) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'GLOBAL',
  `period_yyyymm` char(6) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'YYYYMM',
  `monthly_limit` int unsigned NOT NULL,
  `used_count` int unsigned NOT NULL DEFAULT '0' COMMENT 'Merged api_usage_counters table',
  `last_used_at` datetime DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`api_usage_quota_id`),
  UNIQUE KEY `uq_api_quota_config_scope_period` (`api_config_id`,`campus_scope_key`,`period_yyyymm`),
  KEY `idx_api_quota_campus_period` (`campus_id`,`period_yyyymm`),
  KEY `idx_api_quota_period` (`period_yyyymm`),
  CONSTRAINT `fk_api_quota_campus` FOREIGN KEY (`campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_api_quota_config` FOREIGN KEY (`api_config_id`) REFERENCES `api_configurations` (`api_config_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='API quota + counter per campus/month';

-- Dumping data for table pems_db.api_usage_quotas: ~2 rows (approximately)

-- Dumping structure for table pems_db.audit_log_changes
DROP TABLE IF EXISTS `audit_log_changes`;
CREATE TABLE IF NOT EXISTS `audit_log_changes` (
  `audit_log_change_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `audit_log_id` bigint unsigned NOT NULL,
  `field_name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `change_category` varchar(40) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `value_format` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'TEXT',
  `is_sensitive` tinyint(1) NOT NULL DEFAULT '0',
  `display_order` int unsigned NOT NULL DEFAULT '0',
  `old_value_text` longtext COLLATE utf8mb4_unicode_ci,
  `new_value_text` longtext COLLATE utf8mb4_unicode_ci,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`audit_log_change_id`),
  KEY `idx_audit_changes_log` (`audit_log_id`),
  KEY `idx_audit_changes_field` (`field_name`),
  CONSTRAINT `fk_audit_changes_log` FOREIGN KEY (`audit_log_id`) REFERENCES `audit_logs` (`audit_log_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=99742 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Field-level audit changes; replaces audit_logs old/new JSON values.';

-- Dumping data for table pems_db.audit_log_changes: ~350 rows (approximately)

-- Dumping structure for table pems_db.audit_logs
DROP TABLE IF EXISTS `audit_logs`;
CREATE TABLE IF NOT EXISTS `audit_logs` (
  `audit_log_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `actor_user_id` bigint unsigned DEFAULT NULL,
  `campus_id` bigint unsigned DEFAULT NULL,
  `action` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `entity_type` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `entity_id` bigint unsigned DEFAULT NULL,
  `ip_address` varchar(45) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `user_agent` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `request_id` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `correlation_id` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `visit_request_id` bigint unsigned DEFAULT NULL,
  `visit_instance_id` bigint unsigned DEFAULT NULL,
  `source_type` varchar(80) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `source_id` bigint unsigned DEFAULT NULL,
  `reason` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`audit_log_id`),
  KEY `idx_audit_actor_time` (`actor_user_id`,`created_at`),
  KEY `idx_audit_entity` (`entity_type`,`entity_id`),
  KEY `idx_audit_action_time` (`action`,`created_at`),
  KEY `idx_audit_campus_time` (`campus_id`,`created_at`),
  KEY `idx_audit_request` (`request_id`),
  KEY `idx_audit_visit_request_time` (`visit_request_id`,`created_at`),
  KEY `idx_audit_visit_instance_time` (`visit_instance_id`,`created_at`),
  KEY `idx_audit_correlation` (`correlation_id`),
  KEY `idx_audit_source` (`source_type`,`source_id`),
  CONSTRAINT `fk_audit_actor` FOREIGN KEY (`actor_user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_audit_campus` FOREIGN KEY (`campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=99714 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='General audit log';

-- Dumping data for table pems_db.audit_logs: ~720 rows (approximately)

-- Dumping structure for table pems_db.business_card_ocr_jobs
DROP TABLE IF EXISTS `business_card_ocr_jobs`;
CREATE TABLE IF NOT EXISTS `business_card_ocr_jobs` (
  `ocr_job_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `scanned_card_file_id` bigint unsigned NOT NULL,
  `api_config_id` bigint unsigned NOT NULL,
  `status` enum('UPLOADED','PROCESSING','SUCCEEDED','FAILED','CONFIRMED','DISCARDED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'UPLOADED',
  `provider_name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'GOOGLE_DOCUMENT_AI',
  `provider_request_id` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `provider_processor_id` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `provider_location` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `raw_text_encrypted` longtext COLLATE utf8mb4_unicode_ci,
  `parsed_json_encrypted` longtext COLLATE utf8mb4_unicode_ci,
  `parsed_full_name` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `parsed_email` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `parsed_phone` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `parsed_job_title` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `parsed_department_name` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `parsed_organization` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `parsed_website_url` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `parsed_address` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `confidence_score` decimal(5,2) DEFAULT NULL,
  `matched_partner_id` bigint unsigned DEFAULT NULL,
  `confirmed_partner_id` bigint unsigned DEFAULT NULL,
  `confirmed_contact_id` bigint unsigned DEFAULT NULL,
  `source_visit_instance_id` bigint unsigned DEFAULT NULL,
  `source_guest_member_id` bigint unsigned DEFAULT NULL,
  `source_minute_participant_id` bigint unsigned DEFAULT NULL,
  `file_sha256` char(64) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `error_code` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `error_message` text COLLATE utf8mb4_unicode_ci,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `processed_at` datetime DEFAULT NULL,
  `confirmed_at` datetime DEFAULT NULL,
  `confirmed_by` bigint unsigned DEFAULT NULL,
  `expires_at` datetime DEFAULT NULL,
  `deleted_at` datetime DEFAULT NULL,
  PRIMARY KEY (`ocr_job_id`),
  KEY `idx_bc_ocr_file` (`scanned_card_file_id`),
  KEY `idx_bc_ocr_config_status` (`api_config_id`,`status`),
  KEY `idx_bc_ocr_status_time` (`status`,`created_at`),
  KEY `idx_bc_ocr_file_hash` (`file_sha256`),
  KEY `idx_bc_ocr_matched_partner` (`matched_partner_id`),
  KEY `idx_bc_ocr_confirmed_partner` (`confirmed_partner_id`),
  KEY `idx_bc_ocr_confirmed_contact` (`confirmed_contact_id`),
  KEY `idx_bc_ocr_visit_context` (`source_visit_instance_id`,`source_guest_member_id`,`source_minute_participant_id`),
  KEY `idx_bc_ocr_created_by_time` (`created_by`,`created_at`),
  KEY `fk_bc_ocr_guest_member` (`source_guest_member_id`),
  KEY `fk_bc_ocr_minute_participant` (`source_minute_participant_id`),
  KEY `fk_bc_ocr_confirmed_by` (`confirmed_by`),
  CONSTRAINT `fk_bc_ocr_api_config` FOREIGN KEY (`api_config_id`) REFERENCES `api_configurations` (`api_config_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_bc_ocr_confirmed_by` FOREIGN KEY (`confirmed_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_bc_ocr_confirmed_contact` FOREIGN KEY (`confirmed_contact_id`) REFERENCES `partner_contacts` (`contact_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_bc_ocr_confirmed_partner` FOREIGN KEY (`confirmed_partner_id`) REFERENCES `partners` (`partner_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_bc_ocr_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_bc_ocr_file` FOREIGN KEY (`scanned_card_file_id`) REFERENCES `files` (`file_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_bc_ocr_guest_member` FOREIGN KEY (`source_guest_member_id`) REFERENCES `visit_guest_members` (`guest_member_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_bc_ocr_matched_partner` FOREIGN KEY (`matched_partner_id`) REFERENCES `partners` (`partner_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_bc_ocr_minute_participant` FOREIGN KEY (`source_minute_participant_id`) REFERENCES `minute_participants` (`minute_participant_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_bc_ocr_visit_instance` FOREIGN KEY (`source_visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_instance_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=15 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Cloud OCR job for business cards. OCR draft must be reviewed before creating partner_contacts.';

-- Dumping data for table pems_db.business_card_ocr_jobs: ~14 rows (approximately)

-- Dumping structure for table pems_db.calendar_event_attendees
DROP TABLE IF EXISTS `calendar_event_attendees`;
CREATE TABLE IF NOT EXISTS `calendar_event_attendees` (
  `calendar_event_attendee_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `calendar_event_id` bigint unsigned NOT NULL,
  `user_id` bigint unsigned DEFAULT NULL,
  `attendee_email` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `attendee_name` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `attendee_role` varchar(80) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `response_status` enum('NEEDS_ACTION','ACCEPTED','DECLINED','TENTATIVE') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'NEEDS_ACTION',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`calendar_event_attendee_id`),
  KEY `idx_calendar_attendees_event` (`calendar_event_id`),
  KEY `idx_calendar_attendees_user` (`user_id`),
  KEY `idx_calendar_attendees_email` (`attendee_email`),
  CONSTRAINT `fk_calendar_attendees_event` FOREIGN KEY (`calendar_event_id`) REFERENCES `calendar_events` (`calendar_event_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_calendar_attendees_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=99211 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Calendar attendees; replaces calendar_events.attendees_json.';

-- Dumping data for table pems_db.calendar_event_attendees: ~15 rows (approximately)

-- Dumping structure for table pems_db.calendar_event_reminders
DROP TABLE IF EXISTS `calendar_event_reminders`;
CREATE TABLE IF NOT EXISTS `calendar_event_reminders` (
  `calendar_event_reminder_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `calendar_event_id` bigint unsigned NOT NULL,
  `reminder_type` enum('EMAIL','POPUP','IN_APP') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'IN_APP',
  `minutes_before` int unsigned NOT NULL DEFAULT '0',
  `scheduled_at` datetime DEFAULT NULL,
  `sent_at` datetime DEFAULT NULL,
  `status` enum('PENDING','SENT','CANCELLED','FAILED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`calendar_event_reminder_id`),
  KEY `idx_calendar_reminders_event` (`calendar_event_id`),
  KEY `idx_calendar_reminders_status_schedule` (`status`,`scheduled_at`),
  CONSTRAINT `fk_calendar_reminders_event` FOREIGN KEY (`calendar_event_id`) REFERENCES `calendar_events` (`calendar_event_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=99308 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Calendar reminders; replaces calendar_events.reminders_json.';

-- Dumping data for table pems_db.calendar_event_reminders: ~12 rows (approximately)

-- Dumping structure for table pems_db.calendar_events
DROP TABLE IF EXISTS `calendar_events`;
CREATE TABLE IF NOT EXISTS `calendar_events` (
  `calendar_event_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `owner_user_id` bigint unsigned NOT NULL,
  `campus_id` bigint unsigned DEFAULT NULL,
  `visit_instance_id` bigint unsigned DEFAULT NULL,
  `logistics_item_id` bigint unsigned DEFAULT NULL,
  `source_type` enum('PERSONAL','VISIT','LOGISTICS','DEADLINE') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PERSONAL',
  `title` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` text COLLATE utf8mb4_unicode_ci,
  `location` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `start_at` datetime NOT NULL,
  `end_at` datetime NOT NULL,
  `timezone` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'Asia/Ho_Chi_Minh',
  `is_all_day` tinyint(1) NOT NULL DEFAULT '0',
  `recurrence_rule` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `visibility` enum('PRIVATE','INTERNAL') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PRIVATE',
  `status` enum('ACTIVE','CANCELLED','DONE') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  `deleted_at` datetime DEFAULT NULL,
  `deleted_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`calendar_event_id`),
  KEY `idx_calendar_owner_time` (`owner_user_id`,`start_at`),
  KEY `idx_calendar_campus_time` (`campus_id`,`start_at`),
  KEY `idx_calendar_visit` (`visit_instance_id`),
  KEY `idx_calendar_logistics` (`logistics_item_id`),
  KEY `idx_calendar_source_status_time` (`source_type`,`status`,`start_at`),
  CONSTRAINT `fk_calendar_campus` FOREIGN KEY (`campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_calendar_logistics` FOREIGN KEY (`logistics_item_id`) REFERENCES `visit_logistics_items` (`logistics_item_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_calendar_owner` FOREIGN KEY (`owner_user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_calendar_visit` FOREIGN KEY (`visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_instance_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `calendar_events_chk_1` CHECK ((`end_at` > `start_at`))
) ENGINE=InnoDB AUTO_INCREMENT=99018 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Calendar events. Attendees/reminders are normalized in child tables.';

-- Dumping data for table pems_db.calendar_events: ~19 rows (approximately)

-- Dumping structure for table pems_db.campuses
DROP TABLE IF EXISTS `campuses`;
CREATE TABLE IF NOT EXISTS `campuses` (
  `campus_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `campus_code` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'HN, HCM, DN, CT, QN',
  `name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `city` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `address` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `phone` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `email` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ic_head_user_id` bigint unsigned DEFAULT NULL COMMENT 'FK added after users table',
  `status` enum('ACTIVE','INACTIVE') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`campus_id`),
  UNIQUE KEY `uq_campuses_code` (`campus_code`),
  KEY `idx_campuses_status` (`status`),
  KEY `idx_campuses_city_status` (`city`,`status`),
  KEY `idx_campuses_ic_head` (`ic_head_user_id`)
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Danh mục campus';

-- Dumping data for table pems_db.campuses: ~5 rows (approximately)
INSERT IGNORE INTO `campuses` (`campus_id`, `campus_code`, `name`, `city`, `address`, `phone`, `email`, `ic_head_user_id`, `status`, `created_at`, `created_by`, `updated_at`, `updated_by`) VALUES
	(1, 'HN', 'FPT University Hà Nội', 'Hà Nội', 'Khu Công nghệ cao Hòa Lạc, Thạch Thất, Hà Nội', '02473005588', 'ic.hn@fpt.edu.vn', 3, 'ACTIVE', '2026-01-06 09:00:00', NULL, '2026-02-02 08:00:00', 2),
	(2, 'HCM', 'FPT University TP.HCM', 'TP.HCM', 'Lô E2a-7, Đường D1, Khu Công nghệ cao, TP. Thủ Đức', '02873005588', 'ic.hcm@fpt.edu.vn', 9, 'ACTIVE', '2026-01-06 09:05:00', NULL, '2026-02-02 08:05:00', 2),
	(3, 'DN', 'FPT University Đà Nẵng', 'Đà Nẵng', 'Khu đô thị FPT City, Ngũ Hành Sơn, Đà Nẵng', '02367300558', 'ic.dn@fpt.edu.vn', 11, 'ACTIVE', '2026-01-06 09:10:00', NULL, '2026-02-02 08:10:00', 2),
	(4, 'CT', 'FPT University Cần Thơ', 'Cần Thơ', 'Số 600 Nguyễn Văn Cừ nối dài, Ninh Kiều, Cần Thơ', '02927300558', 'ic.ct@fpt.edu.vn', 13, 'ACTIVE', '2026-01-06 09:15:00', NULL, '2026-02-02 08:15:00', 2),
	(5, 'QN', 'FPT University Quy Nhơn', 'Quy Nhơn', 'Khu đô thị mới An Phú Thịnh, Quy Nhơn, Bình Định', '02567300558', 'ic.qn@fpt.edu.vn', 15, 'ACTIVE', '2026-01-06 09:20:00', NULL, '2026-02-02 08:20:00', 2);

-- Dumping structure for table pems_db.departments
DROP TABLE IF EXISTS `departments`;
CREATE TABLE IF NOT EXISTS `departments` (
  `department_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `campus_id` bigint unsigned NOT NULL,
  `name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `department_type` enum('IC','GENERAL') COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'IC=International Cooperation; GENERAL=other departments',
  `head_user_id` bigint unsigned DEFAULT NULL COMMENT 'FK added after users table',
  `status` enum('ACTIVE','INACTIVE') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`department_id`),
  UNIQUE KEY `uq_departments_campus_name` (`campus_id`,`name`),
  KEY `idx_departments_campus_type` (`campus_id`,`department_type`),
  KEY `idx_departments_status` (`status`),
  KEY `idx_departments_head` (`head_user_id`),
  CONSTRAINT `fk_departments_campus` FOREIGN KEY (`campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=116 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Phòng ban theo campus. STAFF thuộc IC, DEPARTMENT thuộc GENERAL';

-- Dumping data for table pems_db.departments: ~34 rows (approximately)
INSERT IGNORE INTO `departments` (`department_id`, `campus_id`, `name`, `department_type`, `head_user_id`, `status`, `created_at`, `created_by`, `updated_at`, `updated_by`) VALUES
(1, 1, 'Phòng Hợp tác Quốc tế', 'IC', 3, 'ACTIVE', '2026-01-06 10:00:00', NULL, '2026-02-02 09:00:00', 2),
(2, 1, 'Phòng Đào tạo', 'GENERAL', 5, 'ACTIVE', '2026-01-06 10:05:00', NULL, '2026-08-12 16:06:05', 6),
(3, 1, 'Phòng Dịch vụ Cơ sở vật chất', 'GENERAL', 17, 'ACTIVE', '2026-01-06 10:10:00', NULL, '2026-08-18 20:54:43', 18),
(4, 1, 'Phòng Tài chính - Kế toán', 'GENERAL', 242, 'ACTIVE', '2026-01-06 10:15:00', NULL, '2026-08-24 11:59:46', 3),
(5, 1, 'Phòng Truyền thông & Thương hiệu', 'GENERAL', NULL, 'ACTIVE', '2026-01-06 10:20:00', NULL, '2026-08-18 11:59:40', 3),
(6, 2, 'Phòng Hợp tác Quốc tế', 'IC', 9, 'ACTIVE', '2026-01-06 10:25:00', NULL, '2026-02-02 09:15:00', 2),
(11, 3, 'Phòng Đào tạo Đà Nẵng', 'GENERAL', NULL, 'ACTIVE', '2026-01-06 10:50:00', NULL, NULL, NULL),
(14, 4, 'Phòng Công tác Sinh viên Cần Thơ', 'GENERAL', NULL, 'ACTIVE', '2026-01-06 11:05:00', NULL, NULL, NULL),
(17, 5, 'Trung tâm Du lịch - Khách sạn', 'GENERAL', NULL, 'ACTIVE', '2026-01-06 11:20:00', NULL, NULL, NULL),
(19, 1, 'Tổ Lưu trữ Sự kiện cũ', 'GENERAL', NULL, 'INACTIVE', '2026-01-06 11:30:00', NULL, NULL, NULL);

-- Dumping structure for table pems_db.documents
DROP TABLE IF EXISTS `documents`;
CREATE TABLE IF NOT EXISTS `documents` (
  `document_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `file_id` bigint unsigned NOT NULL,
  `owner_type` enum('GENERAL','VISIT','PARTNER','MINUTES','NEWS','LOGISTICS','REPORT','VISIT_INSTANCE_MEDIA') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'GENERAL',
  `owner_id` bigint unsigned DEFAULT NULL,
  `campus_id` bigint unsigned DEFAULT NULL,
  `title` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` text COLLATE utf8mb4_unicode_ci,
  `document_category` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `status` enum('DRAFT','PUBLISHED','ARCHIVED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'DRAFT',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`document_id`),
  KEY `idx_documents_owner` (`owner_type`,`owner_id`),
  KEY `idx_documents_campus_status` (`campus_id`,`status`),
  KEY `idx_documents_category_status` (`document_category`,`status`),
  KEY `idx_documents_created_by_time` (`created_by`,`created_at`),
  KEY `fk_documents_file` (`file_id`),
  FULLTEXT KEY `ft_documents_search` (`title`,`description`),
  CONSTRAINT `fk_documents_campus` FOREIGN KEY (`campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_documents_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_documents_file` FOREIGN KEY (`file_id`) REFERENCES `files` (`file_id`) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=42 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Tài liệu nghiệp vụ. partner_documents/reports/logistics documents merged by owner_type.';

-- Dumping data for table pems_db.documents: ~41 rows (approximately)

-- Dumping structure for table pems_db.email_action_tokens
DROP TABLE IF EXISTS `email_action_tokens`;
CREATE TABLE IF NOT EXISTS `email_action_tokens` (
  `email_action_token_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `token_hash` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Hash của token trong link email; không lưu token raw',
  `action_group_key` varchar(180) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Nhóm các nút cùng một quyết định, ví dụ PARTICIPATION:123:user@example.com',
  `action_context` enum('PARTICIPATION_RESPONSE','PARTICIPATION_ASSIGNMENT_RESPONSE','LOGISTICS_REQUEST_RESPONSE','LOGISTICS_ASSIGNEE_RESPONSE','LOGISTICS_NEGOTIATION','LOGISTICS_PROPOSAL_RESPONSE','LOGISTICS_HANDOVER_SIGNATURE','VISIT_CONTACT_CLAIM','VISIT_CONTACT_TRANSFER') COLLATE utf8mb4_unicode_ci NOT NULL,
  `target_type` enum('VISIT_PARTICIPANT','LOGISTICS_ITEM','LOGISTICS_HANDOVER','VISIT_REQUEST_IDENTITY_CHANGE') COLLATE utf8mb4_unicode_ci NOT NULL,
  `target_id` bigint unsigned NOT NULL,
  `intended_action` enum('ACCEPT','DECLINE','NEGOTIATE','APPROVE_PROPOSAL','REJECT_PROPOSAL','CONFIRM_BORROW','CONFIRM_RETURN') COLLATE utf8mb4_unicode_ci NOT NULL,
  `recipient_user_id` bigint unsigned DEFAULT NULL,
  `recipient_email` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `sent_email_id` bigint unsigned DEFAULT NULL,
  `sent_email_recipient_id` bigint unsigned DEFAULT NULL,
  `expires_at` datetime NOT NULL,
  `used_at` datetime DEFAULT NULL,
  `used_action` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `result_status` enum('PENDING','SUCCESS','ALREADY_RESPONDED','EXPIRED','INVALID','FAILED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING',
  `result_message` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `used_ip` varchar(45) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `used_user_agent` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`email_action_token_id`),
  UNIQUE KEY `uq_email_action_token_hash` (`token_hash`),
  KEY `idx_email_action_group_used` (`action_group_key`,`used_at`),
  KEY `idx_email_action_target` (`target_type`,`target_id`),
  KEY `idx_email_action_recipient` (`recipient_email`),
  KEY `idx_email_action_context_status` (`action_context`,`result_status`),
  KEY `idx_email_action_expires` (`expires_at`),
  KEY `idx_email_action_sent_email` (`sent_email_id`),
  KEY `idx_email_action_sent_recipient` (`sent_email_recipient_id`),
  KEY `idx_email_action_recipient_user` (`recipient_user_id`),
  CONSTRAINT `fk_email_action_recipient_user` FOREIGN KEY (`recipient_user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_email_action_sent_email` FOREIGN KEY (`sent_email_id`) REFERENCES `sent_emails` (`sent_email_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_email_action_sent_recipient` FOREIGN KEY (`sent_email_recipient_id`) REFERENCES `sent_email_recipients` (`sent_email_recipient_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=66157 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='One-time action tokens for email buttons: accept, decline, negotiate, handover signature.';

-- Dumping data for table pems_db.email_action_tokens: ~148 rows (approximately)

-- Dumping structure for table pems_db.email_contact_policies
DROP TABLE IF EXISTS `email_contact_policies`;
CREATE TABLE IF NOT EXISTS `email_contact_policies` (
  `email_contact_policy_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `scope_type` enum('TEMPLATE','CAMPUS','DEPARTMENT','SYSTEM') COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Cấp trong chuỗi kế thừa',
  `scope_key` varchar(64) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'template_code / campus_id / department_id dạng chuỗi; NULL cho dòng SYSTEM duy nhất',
  `requirement` enum('NONE','OPTIONAL','REQUIRED') COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'NONE=không hiển thị; OPTIONAL=hiện nếu tìm được; REQUIRED=hiện, không tìm được thì chặn gửi',
  `contact_source` enum('HOST','SENDER','HOST_THEN_SENDER','CAMPUS_DEFAULT','DEPARTMENT_DEFAULT','SUPPORT_CONTACT') COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Nguồn tra cứu đầu mối. Là enum chứ không phải user_id: đầu mối đúng phụ thuộc chuyến thăm và cơ sở',
  `show_email` tinyint(1) DEFAULT NULL COMMENT 'NULL = kế thừa cấp dưới',
  `show_phone` tinyint(1) DEFAULT NULL COMMENT 'NULL = kế thừa cấp dưới',
  `show_department` tinyint(1) DEFAULT NULL COMMENT 'NULL = kế thừa cấp dưới',
  `show_campus` tinyint(1) DEFAULT NULL COMMENT 'NULL = kế thừa cấp dưới',
  `show_sender` tinyint(1) DEFAULT NULL COMMENT 'Dòng "Được gửi bởi"; NULL = kế thừa cấp dưới',
  `heading_vi` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `heading_en` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `reply_to_source` enum('NONE','CONTACT','SENDER') COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Địa chỉ đặt vào header Reply-To của thư',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`email_contact_policy_id`),
  UNIQUE KEY `uq_email_contact_policies_scope` (`scope_type`,`scope_key`),
  KEY `fk_email_contact_policies_created_by` (`created_by`),
  KEY `fk_email_contact_policies_updated_by` (`updated_by`),
  CONSTRAINT `fk_email_contact_policies_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_email_contact_policies_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=35 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Chính sách hiển thị khối thông tin liên hệ trong email. Chỉ lưu chính sách, không lưu dữ liệu liên hệ.';

-- Dumping data for table pems_db.email_contact_policies: ~34 rows (approximately)

-- Dumping structure for table pems_db.email_send_idempotency
DROP TABLE IF EXISTS `email_send_idempotency`;
CREATE TABLE IF NOT EXISTS `email_send_idempotency` (
  `email_send_idempotency_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `actor_user_id` bigint unsigned NOT NULL COMMENT 'Người bấm gửi, đọc từ JWT đã xác thực — không bao giờ từ payload',
  `operation_code` varchar(64) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Một trong sáu hành động gửi báo cáo/hóa đơn, ví dụ REPORT_HO_CAMPUS',
  `idempotency_key_hash` char(64) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'SHA-256 (hex) của Idempotency-Key — KHÔNG lưu key gốc',
  `request_fingerprint` char(64) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'SHA-256 (hex) của nội dung nghiệp vụ đã chuẩn hoá; cùng key khác fingerprint = từ chối, không gửi',
  `state` varchar(32) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'RESERVED' COMMENT 'RESERVED / PREPARING / DISPATCHING / SUCCEEDED / FAILED_BEFORE_DISPATCH / OUTCOME_UNKNOWN',
  `sent_email_id` bigint unsigned DEFAULT NULL COMMENT 'Bản ghi lịch sử của lần gửi thành công, để đối chiếu',
  `result_message` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Thông báo thành công, phát lại nguyên văn cho lần gọi trùng',
  `failure_code` varchar(64) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Mã lỗi ổn định; không chứa địa chỉ, số tiền, token hay nội dung thư',
  `attempt_count` int unsigned NOT NULL DEFAULT '0' COMMENT 'Số lần handler thực sự chạy dưới key này (chỉ tăng khi retry sau FAILED_BEFORE_DISPATCH)',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `dispatch_started_at` datetime DEFAULT NULL COMMENT 'Thời điểm ngay trước lời gọi ra ngoài; có giá trị nghĩa là không thể khẳng định chưa gửi',
  `completed_at` datetime DEFAULT NULL,
  PRIMARY KEY (`email_send_idempotency_id`),
  UNIQUE KEY `uq_email_send_idempotency_actor_op_key` (`actor_user_id`,`operation_code`,`idempotency_key_hash`),
  KEY `idx_email_send_idempotency_state` (`state`,`created_at`),
  KEY `idx_email_send_idempotency_sent_email` (`sent_email_id`),
  CONSTRAINT `fk_email_send_idempotency_actor` FOREIGN KEY (`actor_user_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_email_send_idempotency_sent_email` FOREIGN KEY (`sent_email_id`) REFERENCES `sent_emails` (`sent_email_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `chk_email_send_idempotency_state` CHECK ((`state` in (_utf8mb4'RESERVED',_utf8mb4'PREPARING',_utf8mb4'DISPATCHING',_utf8mb4'SUCCEEDED',_utf8mb4'FAILED_BEFORE_DISPATCH',_utf8mb4'OUTCOME_UNKNOWN')))
) ENGINE=InnoDB AUTO_INCREMENT=15 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Chống gửi trùng cho sáu hành động gửi báo cáo/hóa đơn (G11 / R-103). Chỉ lưu hash của key và của yêu cầu.';

-- Dumping data for table pems_db.email_send_idempotency: ~14 rows (approximately)

-- Dumping structure for table pems_db.email_templates
DROP TABLE IF EXISTS `email_templates`;
CREATE TABLE IF NOT EXISTS `email_templates` (
  `email_template_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `template_code` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `purpose` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `campus_id` bigint unsigned DEFAULT NULL,
  `description` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `status` enum('ACTIVE','INACTIVE') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE',
  `subject_vi` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `body_vi` longtext COLLATE utf8mb4_unicode_ci,
  `subject_en` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `body_en` longtext COLLATE utf8mb4_unicode_ci,
  `body_format` enum('PLAIN_TEXT','HTML') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'HTML' COMMENT 'Định dạng nội dung email template; HTML dùng cho rich text editor',
  `variables_text` varchar(700) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `revision` int unsigned NOT NULL DEFAULT '1',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`email_template_id`),
  UNIQUE KEY `uq_email_templates_code` (`template_code`),
  KEY `idx_email_templates_status` (`status`),
  KEY `idx_email_templates_purpose_status` (`purpose`,`status`),
  KEY `idx_email_templates_campus_status` (`campus_id`,`status`),
  CONSTRAINT `fk_email_templates_campus` FOREIGN KEY (`campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=70034 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Email templates with explicit VI/EN subject/body fields';

-- Dumping data for table pems_db.email_templates: ~33 rows (approximately)
INSERT IGNORE INTO `email_templates` (`email_template_id`, `template_code`, `name`, `purpose`, `campus_id`, `description`, `status`, `subject_vi`, `body_vi`, `subject_en`, `body_en`, `body_format`, `variables_text`, `revision`, `created_at`, `created_by`, `updated_at`, `updated_by`) VALUES
	(70001, 'ACCOUNT_EMAIL_CONFIRMATION', 'Xác nhận email để kích hoạt tài khoản', 'ACCOUNT', NULL, 'Gửi cho tài khoản vừa tạo ở trạng thái chờ xác nhận email. Là email đầu tiên của mọi luồng tạo tài khoản.', 'ACTIVE', '[PEMS] Xác nhận email để kích hoạt tài khoản PEMS', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Một tài khoản PEMS đã được khởi tạo cho bạn. Tài khoản đang ở trạng thái chờ xác nhận email và chưa đăng nhập được.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Vai trò</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{roleName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{campusName}}</td></tr></tbody></table><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Cần bạn xác nhận</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Bấm nút bên dưới để xác nhận địa chỉ email này. Sau khi xác nhận, tài khoản sẽ chuyển sang trạng thái hoạt động.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Liên kết xác nhận có hiệu lực trong <strong>{{expiresInHours}}</strong> giờ và chỉ dùng được một lần.</p><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Lưu ý bảo mật:</strong> Không chia sẻ liên kết này với bất kỳ ai. Nếu bạn không mong đợi email này, vui lòng bỏ qua và không bấm vào liên kết.</div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Confirm your email to activate your PEMS account', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">A PEMS account has been created for you. It is pending email confirmation and cannot be signed in to yet.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Role</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{roleName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{campusName}}</td></tr></tbody></table><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Confirmation required</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Use the button below to confirm this email address. The account becomes active once it is confirmed.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The confirmation link is valid for <strong>{{expiresInHours}}</strong> hours and can be used once.</p><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Security note:</strong> Do not share this link with anyone. If you were not expecting this email, please ignore it and do not open the link.</div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'fullName,roleName,campusName,expiresInHours', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70002, 'ACCOUNT_PENDING_EMAIL_CHANGED_OLD_NOTICE', 'Báo địa chỉ cũ khi đổi email tài khoản chờ xác nhận', 'ACCOUNT', NULL, 'Gửi tới địa chỉ cũ khi email của một tài khoản còn chờ xác nhận bị đổi. Cố tình ẩn danh: địa chỉ cũ có thể là người gõ nhầm, không liên quan tới tài khoản.', 'ACTIVE', '[PEMS] Địa chỉ email đăng ký đã được thay đổi', '<p style="margin:0 0 16px;color:#334155">Kính gửi Quý vị,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Một tài khoản PEMS trước đây được đăng ký với địa chỉ email này đã được chuyển sang một địa chỉ khác trước khi kích hoạt.</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Địa chỉ này sẽ không được dùng để đăng nhập. Quý vị không cần thực hiện thêm thao tác nào.</p><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Lưu ý bảo mật:</strong> Nếu Quý vị không biết về tài khoản này hoặc cho rằng đây là sự nhầm lẫn, vui lòng liên hệ người gửi email này hoặc bộ phận quản trị hệ thống PEMS.</div><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] The registration email has been changed', '<p style="margin:0 0 16px;color:#334155">Dear Sir or Madam,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">A PEMS account previously registered with this email address was moved to a different address before activation.</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">This address will not be used to sign in. No action is needed from you.</p><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Security note:</strong> If you do not recognise this account, or believe this is a mistake, please contact the sender of this email or the PEMS system administrator.</div><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70003, 'ACCOUNT_ACTIVATED', 'Thông báo tài khoản đã kích hoạt', 'ACCOUNT', NULL, 'Gửi sau khi người dùng xác nhận email thành công và tài khoản chuyển sang hoạt động.', 'ACTIVE', '[PEMS] Tài khoản PEMS của bạn đã được kích hoạt', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Địa chỉ email của bạn đã được xác nhận. Tài khoản PEMS đã sẵn sàng sử dụng.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Vai trò</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{roleName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{campusName}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Bạn có thể đăng nhập bằng chính địa chỉ email này và sử dụng các chức năng thuộc vai trò được cấp.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Nếu bạn gặp khó khăn khi truy cập hệ thống, vui lòng liên hệ người gửi email này.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Your PEMS account is now active', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Your email address has been confirmed and your PEMS account is ready to use.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Role</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{roleName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{campusName}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">You can sign in with this email address and use the features assigned to your role.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">If you have trouble signing in, please contact the sender of this email.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'fullName,roleName,campusName,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70004, 'ACCOUNT_EMAIL_CHANGED_OLD_NOTICE', 'Báo địa chỉ cũ khi đổi email tài khoản đang hoạt động', 'ACCOUNT', NULL, 'Gửi tới địa chỉ vừa bị gỡ khỏi một tài khoản đang hoạt động. Cố tình ẩn danh: không nêu tên chủ tài khoản, địa chỉ mới, vai trò hay cơ sở.', 'ACTIVE', '[PEMS] Địa chỉ email này đã được gỡ khỏi một tài khoản PEMS', '<p style="margin:0 0 16px;color:#334155">Kính gửi Quý vị,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Địa chỉ email này không còn được dùng để đăng nhập vào tài khoản PEMS đã liên kết trước đó.</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Mọi phiên đăng nhập bằng địa chỉ này đã được thu hồi. Quý vị không cần thực hiện thêm thao tác nào.</p><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Lưu ý bảo mật:</strong> Nếu Quý vị không biết về tài khoản này hoặc cho rằng đây là sự nhầm lẫn, vui lòng liên hệ người gửi email này hoặc bộ phận quản trị hệ thống PEMS.</div><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] This address has been removed from a PEMS account', '<p style="margin:0 0 16px;color:#334155">Dear Sir or Madam,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">This email address is no longer used to sign in to the PEMS account it was previously linked to.</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Any sessions opened with this address have been revoked. No action is needed from you.</p><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Security note:</strong> If you do not recognise this account, or believe this is a mistake, please contact the sender of this email or the PEMS system administrator.</div><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70005, 'ACCOUNT_EMAIL_CHANGED_NEW_NOTICE', 'Báo địa chỉ mới khi đổi email tài khoản đang hoạt động', 'ACCOUNT', NULL, 'Gửi tới địa chỉ mới khi email của một tài khoản đang hoạt động được chuyển sang địa chỉ đó.', 'ACTIVE', '[PEMS] Địa chỉ email này đã được gắn với tài khoản PEMS của bạn', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Địa chỉ email này đã trở thành địa chỉ đăng nhập cho tài khoản PEMS của bạn.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%">Địa chỉ trước đây</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{oldEmailMasked}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Từ lần đăng nhập kế tiếp, vui lòng dùng chính địa chỉ email này. Mọi phiên đăng nhập bằng địa chỉ cũ đã được thu hồi.</p><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Lưu ý bảo mật:</strong> Nếu bạn không yêu cầu thay đổi này, hãy liên hệ ngay người gửi email này hoặc bộ phận quản trị hệ thống PEMS.</div><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] This address is now the sign-in email for your PEMS account', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">This email address is now the sign-in address for your PEMS account.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%">Previous address</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{oldEmailMasked}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Please use this address from your next sign-in. Any sessions opened with the previous address have been revoked.</p><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Security note:</strong> If you did not request this change, contact the sender of this email or the PEMS system administrator immediately.</div><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'fullName,oldEmailMasked,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70006, 'ACCOUNT_ROLE_CHANGED', 'Thông báo thay đổi vai trò tài khoản', 'ACCOUNT', NULL, 'Gửi cho người dùng khi vai trò trên hệ thống của họ được thay đổi.', 'ACTIVE', '[PEMS] Vai trò tài khoản PEMS của bạn đã thay đổi', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Vai trò của bạn trên hệ thống PEMS vừa được cập nhật.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Vai trò trước đây</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{oldRoleName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Vai trò hiện tại</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{newRoleName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{campusName}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Quyền truy cập của bạn thay đổi theo vai trò mới ngay từ lần đăng nhập kế tiếp. Một số màn hình trước đây bạn mở được có thể không còn hiển thị.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Nếu bạn cho rằng thay đổi này chưa đúng, vui lòng phản hồi cho người gửi email này.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Your PEMS account role has changed', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Your role in PEMS has been updated.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Previous role</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{oldRoleName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Current role</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{newRoleName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{campusName}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Your access follows the new role from your next sign-in. Some screens you could open before may no longer appear.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">If this change does not look right, please reply to the sender of this email.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'fullName,oldRoleName,newRoleName,campusName,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70007, 'ACCOUNT_STAFF_LEADER_ASSIGNED', 'Thông báo được phân công làm Staff Leader', 'ACCOUNT', NULL, 'Gửi cho người được phân công làm Staff Leader của một campus.', 'ACTIVE', '[PEMS] Bạn được phân công làm Staff Leader', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Bạn được phân công đảm nhiệm vai trò Staff Leader trên hệ thống PEMS.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Hiệu lực từ</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{effectiveDate}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Lý do phân công</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{reason}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Từ thời điểm hiệu lực, bạn tiếp nhận việc duyệt yêu cầu tham quan của cơ sở này, phân công host và theo dõi tiến độ chuẩn bị.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Nếu thông tin phân công chưa chính xác, vui lòng phản hồi cho người gửi email này.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] You have been assigned as Staff Leader', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">You have been assigned the Staff Leader role in PEMS.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Effective from</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{effectiveDate}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Reason</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{reason}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">From the effective date you take over approving this campus visit requests, assigning hosts and tracking preparation.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">If any of this is incorrect, please reply to the sender of this email.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'fullName,campusName,effectiveDate,reason,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70008, 'ACCOUNT_STAFF_LEADER_REPLACED', 'Thông báo kết thúc vai trò Staff Leader', 'ACCOUNT', NULL, 'Gửi cho Staff Leader cũ khi vai trò của họ được chuyển cho người khác.', 'ACTIVE', '[PEMS] Vai trò Staff Leader của bạn đã kết thúc', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Vai trò Staff Leader của bạn đã được chuyển cho người khác.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Người tiếp nhận</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{successorName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Hiệu lực từ</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{effectiveDate}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Lý do thay đổi</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{reason}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Các yêu cầu đang chờ xử lý của cơ sở này được chuyển sang người tiếp nhận. Bạn vẫn xem được lịch sử công việc đã thực hiện.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Nếu bạn cho rằng thay đổi này chưa đúng, vui lòng phản hồi cho người gửi email này.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Your Staff Leader role has ended', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Your Staff Leader role has been transferred to someone else.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Successor</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{successorName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Effective from</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{effectiveDate}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Reason</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{reason}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Pending requests for this campus move to the successor. You keep read access to the work you already handled.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">If this change does not look right, please reply to the sender of this email.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'fullName,campusName,successorName,effectiveDate,reason,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70009, 'DEPT_PERSONNEL_ACCOUNT_DISABLED', 'Thông báo tài khoản bị vô hiệu hóa', 'ACCOUNT', NULL, 'Gửi cho nhân sự khi Trưởng phòng vô hiệu hóa tài khoản của họ.', 'ACTIVE', '[PEMS] Tài khoản PEMS của bạn đã bị vô hiệu hóa', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Tài khoản PEMS của bạn đã được chuyển sang trạng thái vô hiệu hóa.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Phòng ban</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{departmentName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Lý do</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{reason}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Bạn sẽ không đăng nhập được cho tới khi tài khoản được kích hoạt lại. Các nhiệm vụ đang được giao cho bạn sẽ do phòng ban điều phối lại.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Nếu bạn cho rằng đây là sự nhầm lẫn, vui lòng phản hồi cho người gửi email này.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Your PEMS account has been disabled', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Your PEMS account has been set to disabled.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Department</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{departmentName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Reason</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{reason}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">You will not be able to sign in until the account is enabled again. Tasks currently assigned to you will be reassigned by your department.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">If you believe this is a mistake, please reply to the sender of this email.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'fullName,departmentName,reason,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70010, 'DEPT_PERSONNEL_ACCOUNT_ENABLED', 'Thông báo tài khoản được kích hoạt lại', 'ACCOUNT', NULL, 'Gửi cho nhân sự khi Trưởng phòng kích hoạt lại tài khoản của họ.', 'ACTIVE', '[PEMS] Tài khoản PEMS của bạn đã được kích hoạt lại', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Tài khoản PEMS của bạn đã được kích hoạt lại và có thể đăng nhập bình thường.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%">Phòng ban</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{departmentName}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Bạn tiếp tục nhận và xử lý các nhiệm vụ thuộc phòng ban như trước.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Your PEMS account has been re-enabled', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Your PEMS account has been re-enabled and you can sign in as usual.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%">Department</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{departmentName}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">You will continue to receive and handle your department tasks as before.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'fullName,departmentName,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70011, 'DEPT_LEADERSHIP_GRANTED', 'Thông báo được bổ nhiệm Trưởng phòng', 'ACCOUNT', NULL, 'Gửi cho người nhận vai trò Trưởng phòng sau khi bàn giao được ghi nhận.', 'ACTIVE', '[PEMS] Bạn được bổ nhiệm làm Trưởng phòng', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Việc bàn giao vai trò Trưởng phòng đã được ghi nhận trên hệ thống PEMS. Bạn là Trưởng phòng đương nhiệm.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%">Phòng ban</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{departmentName}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Từ bây giờ bạn tiếp nhận các yêu cầu hậu cần gửi tới phòng ban, phân công nhân sự xử lý và theo dõi tiến độ.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Nếu thông tin bàn giao chưa chính xác, vui lòng phản hồi cho người gửi email này.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] You have been appointed Department Leader', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">The Department Leader handover has been recorded in PEMS. You are the current Department Leader.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%">Department</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{departmentName}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">You now receive logistics requests addressed to the department, assign staff to handle them and track progress.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">If the handover details are incorrect, please reply to the sender of this email.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'fullName,departmentName,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70012, 'DEPT_LEADERSHIP_HANDED_OVER', 'Thông báo đã bàn giao vai trò Trưởng phòng', 'ACCOUNT', NULL, 'Gửi cho Trưởng phòng cũ sau khi bàn giao được ghi nhận.', 'ACTIVE', '[PEMS] Bạn đã bàn giao vai trò Trưởng phòng', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Việc bàn giao vai trò Trưởng phòng của bạn đã được ghi nhận trên hệ thống PEMS.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%">Phòng ban</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{departmentName}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Các yêu cầu hậu cần mới sẽ được chuyển tới Trưởng phòng đương nhiệm. Bạn vẫn xem được lịch sử công việc đã thực hiện.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Nếu thông tin bàn giao chưa chính xác, vui lòng phản hồi cho người gửi email này.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] You have handed over the Department Leader role', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Your handover of the Department Leader role has been recorded in PEMS.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%">Department</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{departmentName}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">New logistics requests will go to the current Department Leader. You keep read access to the work you already handled.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">If the handover details are incorrect, please reply to the sender of this email.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'fullName,departmentName,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70013, 'AUTH_PASSWORD_RESET_OTP', 'Mã đặt lại mật khẩu', 'AUTH', NULL, 'Gửi mã OTP đặt lại mật khẩu. Chỉ gửi riêng cho một người nhận, cấm CC/BCC.', 'ACTIVE', '[PEMS] Mã đặt lại mật khẩu', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản PEMS của bạn. Dùng mã dưới đây để tiếp tục.</p><div style="margin:18px 0;padding:18px;background:#f8fafc;border:1px dashed #cbd5e1;border-radius:10px;text-align:center"><span style="font-family:Courier New,monospace;font-size:28px;font-weight:700;color:#0f3d67;letter-spacing:6px">{{otpCode}}</span></div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Mã có hiệu lực trong <strong>{{expireMinutes}}</strong> phút và chỉ dùng được một lần.</p><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Lưu ý bảo mật:</strong> Không chia sẻ mã này với bất kỳ ai, kể cả người tự nhận là nhân sự PEMS. Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này; mật khẩu hiện tại vẫn giữ nguyên.</div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Your password reset code', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">We received a request to reset the password for your PEMS account. Use the code below to continue.</p><div style="margin:18px 0;padding:18px;background:#f8fafc;border:1px dashed #cbd5e1;border-radius:10px;text-align:center"><span style="font-family:Courier New,monospace;font-size:28px;font-weight:700;color:#0f3d67;letter-spacing:6px">{{otpCode}}</span></div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The code is valid for <strong>{{expireMinutes}}</strong> minutes and can be used once.</p><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Security note:</strong> Do not share this code with anyone, including someone claiming to be PEMS staff. If you did not request a password reset, ignore this email; your current password stays unchanged.</div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'fullName,otpCode,expireMinutes', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70014, 'VISIT_REQUEST_OTP', 'Mã xác thực đăng ký tham quan', 'VISIT_REQUEST', NULL, 'Gửi mã OTP xác thực email cho người đăng ký tham quan trước khi tạo yêu cầu. Cấm CC/BCC.', 'ACTIVE', '[PEMS] Mã xác thực email đăng ký tham quan', '<p style="margin:0 0 16px;color:#334155">Kính gửi <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Cảm ơn Quý vị đã gửi đăng ký tham quan tới FPT University. Vui lòng dùng mã dưới đây để xác thực địa chỉ email trước khi yêu cầu được tạo.</p><div style="margin:18px 0;padding:18px;background:#f8fafc;border:1px dashed #cbd5e1;border-radius:10px;text-align:center"><span style="font-family:Courier New,monospace;font-size:28px;font-weight:700;color:#0f3d67;letter-spacing:6px">{{otpCode}}</span></div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Mã có hiệu lực trong <strong>{{expireMinutes}}</strong> phút và chỉ dùng được một lần.</p><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Lưu ý bảo mật:</strong> Không chia sẻ mã này với bất kỳ ai. Nếu Quý vị không gửi đăng ký nào, vui lòng bỏ qua email này.</div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Verification code for your visit registration', '<p style="margin:0 0 16px;color:#334155">Dear <strong>{{fullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Thank you for registering a visit to FPT University. Please use the code below to verify this email address before the request is created.</p><div style="margin:18px 0;padding:18px;background:#f8fafc;border:1px dashed #cbd5e1;border-radius:10px;text-align:center"><span style="font-family:Courier New,monospace;font-size:28px;font-weight:700;color:#0f3d67;letter-spacing:6px">{{otpCode}}</span></div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The code is valid for <strong>{{expireMinutes}}</strong> minutes and can be used once.</p><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Security note:</strong> Do not share this code with anyone. If you did not submit a registration, please ignore this email.</div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'fullName,otpCode,expireMinutes', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70015, 'VISIT_CONTACT_CLAIM', 'Xác nhận vai trò đầu mối liên hệ', 'VISIT_REQUEST', NULL, 'Gửi cho người được ghi nhận là đầu mối liên hệ của một yêu cầu tham quan để họ xác nhận vai trò. Có liên kết dùng một lần.', 'ACTIVE', '[PEMS] Xác nhận vai trò đầu mối liên hệ cho yêu cầu {{requestCode}}', '<p style="margin:0 0 16px;color:#334155">Kính gửi <strong>{{contactFullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Quý vị được ghi nhận là đầu mối liên hệ của một yêu cầu tham quan tại FPT University. Chúng tôi cần Quý vị xác nhận vai trò này trước khi trao đổi các thông tin tiếp theo.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Mã yêu cầu</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{requestCode}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Đoàn</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Thời gian dự kiến</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{plannedTime}}</td></tr></tbody></table><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Cần Quý vị xác nhận</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Vui lòng dùng các nút bên dưới để chấp nhận hoặc từ chối vai trò đầu mối liên hệ. Quý vị không cần đăng nhập PEMS. Mỗi nút mở một trang xác nhận bằng liên kết dùng một lần để Quý vị xem thông tin chuyến thăm mới nhất trước khi quyết định.</p>{{actionBlock}}</div><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Lưu ý bảo mật:</strong> Các liên kết có hiệu lực đến <strong>{{contactExpiresAt}}</strong> và mỗi liên kết chỉ dùng được một lần. Vui lòng không chuyển tiếp email hoặc các liên kết này cho người khác.</div><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Confirm you are the contact for visit request {{requestCode}}', '<p style="margin:0 0 16px;color:#334155">Dear <strong>{{contactFullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">You have been recorded as the contact person for a visit request at FPT University. We need you to confirm this role before we continue.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Request code</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{requestCode}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Delegation</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Planned time</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{plannedTime}}</td></tr></tbody></table><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Confirmation required</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Use the buttons below to accept or decline the contact role. You do not need to sign in to PEMS. Each button opens a confirmation page using a one-time link, so you can review the latest visit details before deciding.</p>{{actionBlock}}</div><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Security note:</strong> The links are valid until <strong>{{contactExpiresAt}}</strong> and each can be used once. Do not forward the email or its links.</div><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'contactFullName,requestCode,delegationName,campusName,plannedTime,contactExpiresAt,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 3, '2026-08-08 17:06:15', NULL, '2026-08-21 13:19:04', NULL),
	(70016, 'VISIT_CONTACT_TRANSFER', 'Lời mời nhận vai trò đầu mối liên hệ', 'VISIT_REQUEST', NULL, 'Gửi cho người được đề nghị tiếp nhận vai trò đầu mối liên hệ từ người hiện tại. Có liên kết dùng một lần.', 'ACTIVE', '[PEMS] Lời mời nhận vai trò đầu mối liên hệ cho yêu cầu {{requestCode}}', '<p style="margin:0 0 16px;color:#334155">Kính gửi <strong>{{contactFullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Quý vị được đề nghị tiếp nhận vai trò đầu mối liên hệ của một yêu cầu tham quan tại FPT University.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Mã yêu cầu</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{requestCode}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Đoàn</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Thời gian dự kiến</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedTime}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Đầu mối hiện tại</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{currentContactName}}</td></tr></tbody></table><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Cần Quý vị phản hồi</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Vui lòng dùng các nút bên dưới để chấp nhận hoặc từ chối. Quý vị không cần đăng nhập PEMS để phản hồi lời mời này. Nếu Quý vị chấp nhận, các trao đổi tiếp theo về yêu cầu này sẽ được gửi tới địa chỉ email này.</p>{{actionBlock}}</div><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Lưu ý bảo mật:</strong> Các liên kết có hiệu lực đến <strong>{{contactExpiresAt}}</strong> và mỗi liên kết chỉ dùng được một lần. Vui lòng không chuyển tiếp email hoặc các liên kết này cho người khác.</div><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Invitation to take over the contact role for request {{requestCode}}', '<p style="margin:0 0 16px;color:#334155">Dear <strong>{{contactFullName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">You have been asked to take over the contact role for a visit request at FPT University.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Request code</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{requestCode}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Delegation</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Planned time</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedTime}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Current contact</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{currentContactName}}</td></tr></tbody></table><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Your response is needed</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Use the buttons below to accept or decline. You do not need to sign in to PEMS to respond. If you accept, further correspondence about this request will be sent to this address.</p>{{actionBlock}}</div><div style="margin:18px 0;padding:14px 16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px;color:#9a3412;line-height:1.6"><strong>Security note:</strong> The links are valid until <strong>{{contactExpiresAt}}</strong> and each can be used once. Please do not forward the email or its links.</div><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'contactFullName,currentContactName,requestCode,delegationName,campusName,plannedTime,contactExpiresAt,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 3, '2026-08-08 17:06:15', NULL, '2026-08-21 13:19:04', NULL),
	(70017, 'VISIT_PARTICIPANT_INVITATION', 'Mời nhân sự tham gia hỗ trợ tiếp khách', 'VISIT_PARTICIPANT', NULL, 'Email mời nhân sự IC tham gia hỗ trợ một chuyến tiếp khách. Mỗi người nhận có liên kết chấp nhận/từ chối riêng.', 'ACTIVE', '[PEMS] Mời tham gia hỗ trợ tiếp đón đoàn {{delegationName}}', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">{{hostName}} mời bạn tham gia hỗ trợ một chuyến tiếp khách tại {{campusName}}.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Đoàn khách</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Thời gian dự kiến</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedTime}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Vai trò của bạn</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{roleLabel}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65"><em>{{hostMessage}}</em></p><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Cần bạn phản hồi</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Vui lòng chọn một phương án bên dưới để chúng tôi chốt danh sách nhân sự tham gia.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Liên kết phản hồi sẽ hết hạn sau 14 ngày và chỉ sử dụng được một lần.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Invitation to support the visit of {{delegationName}}', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">{{hostName}} invites you to support a visit at {{campusName}}.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Delegation</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Planned time</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedTime}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Your role</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{roleLabel}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65"><em>{{hostMessage}}</em></p><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Your response is needed</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Please choose one of the options below so we can confirm the supporting team.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The response link expires in 14 days and can be used once.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'recipientName,delegationName,campusName,plannedTime,hostName,roleLabel,hostMessage,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 4, '2026-08-08 17:06:15', NULL, '2026-08-25 01:23:17', 2),
	(70018, 'VISIT_STUDENT_INVITATION', 'Mời sinh viên hỗ trợ tiếp khách', 'VISIT_PARTICIPANT', NULL, 'Email mời sinh viên hỗ trợ một chuyến tiếp khách. Mỗi người nhận có liên kết chấp nhận/từ chối riêng.', 'ACTIVE', '[PEMS] Mời sinh viên hỗ trợ tiếp đón đoàn {{delegationName}}', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">{{hostName}} mời bạn tham gia hỗ trợ một chuyến tiếp khách tại {{campusName}}. Đây là hoạt động tự nguyện dành cho sinh viên.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Đoàn khách</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Thời gian dự kiến</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedTime}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Vai trò của bạn</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{roleLabel}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65"><em>{{hostMessage}}</em></p><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Cần bạn phản hồi</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Vui lòng chọn một phương án bên dưới. Nếu bạn nhận lời, thông tin tập trung và hướng dẫn chi tiết sẽ được gửi trước ngày diễn ra.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Liên kết phản hồi sẽ hết hạn sau 14 ngày và chỉ sử dụng được một lần.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Student invitation to support the visit of {{delegationName}}', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">{{hostName}} invites you to help host a visit at {{campusName}}. Taking part is voluntary.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Delegation</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Planned time</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedTime}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Your role</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{roleLabel}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65"><em>{{hostMessage}}</em></p><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Your response is needed</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Please choose one of the options below. If you accept, the meeting point and detailed instructions will be sent before the day.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The response link expires in 14 days and can be used once.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'recipientName,delegationName,campusName,plannedTime,hostName,roleLabel,hostMessage,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 4, '2026-08-08 17:06:15', NULL, '2026-08-21 13:19:05', 2),
	(70019, 'VISIT_DEPARTMENT_LEADER_INVITATION', 'Mời phòng ban phối hợp tiếp khách', 'VISIT_PARTICIPANT', NULL, 'Email mời Trưởng phòng ban phối hợp tiếp khách; ngoài chấp nhận/từ chối còn có liên kết gán nhân sự yêu cầu đăng nhập.', 'ACTIVE', '[PEMS] Mời phòng ban phối hợp tiếp đón đoàn {{delegationName}}', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">{{hostName}} đề nghị phòng ban của bạn phối hợp cho một chuyến tiếp khách tại {{campusName}}.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Đoàn khách</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Thời gian dự kiến</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedTime}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Nội dung phối hợp</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{roleLabel}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65"><em>{{hostMessage}}</em></p><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Cần bạn phản hồi</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Vui lòng chọn một phương án bên dưới. Bạn cũng có thể gán trực tiếp một nhân sự của phòng ban để tiếp nhận công việc này.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Liên kết phản hồi sẽ hết hạn sau 14 ngày và chỉ sử dụng được một lần. Thao tác Gán nhân sự yêu cầu đăng nhập hệ thống.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Request for department support for the visit of {{delegationName}}', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">{{hostName}} asks your department to support a visit at {{campusName}}.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Delegation</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Planned time</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedTime}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Support requested</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{roleLabel}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65"><em>{{hostMessage}}</em></p><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Your response is needed</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Please choose one of the options below. You can also assign a member of your department to take this on.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The response link expires in 14 days and can be used once. Assigning a staff member requires signing in to the system.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'recipientName,delegationName,campusName,plannedTime,hostName,roleLabel,hostMessage,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 2, '2026-08-08 17:06:15', NULL, '2026-08-21 13:19:05', NULL),
	(70020, 'VISIT_DEPARTMENT_STAFF_ASSIGNMENT', 'Phân công nhân sự phòng ban hỗ trợ tiếp khách', 'VISIT_PARTICIPANT', NULL, 'Gửi cho nhân sự được Trưởng phòng ban gán vào một chuyến tiếp khách. Khác với lời mời: đây là phân công.', 'ACTIVE', '[PEMS] Bạn được phân công hỗ trợ tiếp đón đoàn {{delegationName}}', '<p><span style="color: rgb(51, 65, 85);">Xin&nbsp;chào&nbsp;</span><strong style="color: rgb(51, 65, 85);">{{recipientName}}</strong><span style="color: rgb(51, 65, 85);">,</span></p><p><span style="color: rgb(51, 65, 85);">Trưởng&nbsp;phòng&nbsp;{{departmentName}}&nbsp;đã&nbsp;phân&nbsp;công&nbsp;bạn&nbsp;hỗ&nbsp;trợ&nbsp;một&nbsp;chuyến&nbsp;tiếp&nbsp;khách&nbsp;tại&nbsp;{{campusName}}.</span></p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Đoàn khách</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Thời gian dự kiến</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedTime}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Phòng ban</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{departmentName}}</td></tr></tbody></table><p><strong style="color: rgb(15, 61, 103);">Cần&nbsp;bạn&nbsp;phản&nbsp;hồi</strong></p><p><span style="color: rgb(51, 65, 85);">Vui&nbsp;lòng&nbsp;chọn&nbsp;một&nbsp;phương&nbsp;án&nbsp;bên&nbsp;dưới&nbsp;để&nbsp;phòng&nbsp;ban&nbsp;biết&nbsp;bạn&nbsp;có&nbsp;nhận&nbsp;nhiệm&nbsp;vụ&nbsp;này&nbsp;hay&nbsp;không.</span></p>{{actionBlock}}<p><span style="font-size: 12px; color: rgb(100, 116, 139);">Liên&nbsp;kết&nbsp;phản&nbsp;hồi&nbsp;sẽ&nbsp;hết&nbsp;hạn&nbsp;sau&nbsp;14&nbsp;ngày&nbsp;và&nbsp;chỉ&nbsp;sử&nbsp;dụng&nbsp;được&nbsp;một&nbsp;lần.</span></p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br>{{senderRole}}<br>{{senderDepartment}}<br>{{senderEmail}}</p></div><p><span style="font-size: 12px; color: rgb(100, 116, 139);">Trân&nbsp;trọng,</span></p><p><strong style="font-size: 12px; color: rgb(100, 116, 139);">PEMS&nbsp;-&nbsp;FPT&nbsp;University</strong></p>', '[PEMS] You have been assigned to support the visit of {{delegationName}}', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">The head of {{departmentName}} has assigned you to support a visit at {{campusName}}.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Delegation</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Planned time</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedTime}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Department</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{departmentName}}</td></tr></tbody></table><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Your response is needed</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Please choose one of the options below so your department knows whether you are taking this on.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The response link expires in 14 days and can be used once.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'recipientName,delegationName,campusName,plannedTime,departmentName,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 3, '2026-08-08 17:06:15', NULL, '2026-08-24 21:52:01', 2),
	(70021, 'VISIT_REMINDER_HOST', 'Nhắc lịch tiếp khách cho người phụ trách', 'VISIT_REMINDER', NULL, 'Nhắc người phụ trách tiếp đón trước giờ tiếp khách theo cấu hình nhắc lịch của chuyến.', 'ACTIVE', '[PEMS] Nhắc lịch tiếp đón đoàn {{delegationName}}', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{hostName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Đây là email nhắc lịch cho chuyến tiếp khách bạn đang phụ trách.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Đoàn khách</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Bắt đầu</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedStart}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Kết thúc</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedEnd}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{campusName}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Trước ngày diễn ra, vui lòng rà soát:</p><ul style="margin:0 0 14px;padding-left:20px;color:#334155;line-height:1.7"><li>Danh sách khách và thành phần phía FPT đã đầy đủ.</li><li>Lịch trình từng mốc thời gian đã được chốt.</li><li>Các hạng mục hậu cần đã được phòng ban xác nhận.</li></ul><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Việc cần làm</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Mở chuyến tiếp khách trong hệ thống để kiểm tra và cập nhật phần còn thiếu.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Liên kết yêu cầu đăng nhập hệ thống PEMS.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Reminder: hosting {{delegationName}}', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{hostName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">This is a reminder for the visit you are hosting.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Delegation</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Starts</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedStart}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Ends</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedEnd}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{campusName}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Before the day, please check that:</p><ul style="margin:0 0 14px;padding-left:20px;color:#334155;line-height:1.7"><li>the guest list and the FPT participants are complete;</li><li>every step of the agenda is confirmed;</li><li>the logistics items have been accepted by the departments.</li></ul><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">What to do</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Open the visit in the system to review it and fill in whatever is still missing.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The link requires signing in to PEMS.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'hostName,delegationName,campusName,plannedStart,plannedEnd,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 2, '2026-08-08 17:06:15', NULL, '2026-08-21 13:19:07', NULL),
	(70022, 'VISIT_REMINDER_PARTICIPANTS', 'Nhắc lịch tiếp khách cho người tham gia', 'VISIT_REMINDER', NULL, 'Nhắc từng người đã nhận lời mời hoặc được gán, trước giờ tiếp khách. Gửi riêng từng người, không liệt kê người khác.', 'ACTIVE', '[PEMS] Nhắc lịch tham gia tiếp đón đoàn {{delegationName}}', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Đây là email nhắc lịch cho chuyến tiếp khách bạn đã nhận lời tham gia.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Đoàn khách</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Bắt đầu</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedStart}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Kết thúc</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedEnd}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{campusName}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Vui lòng có mặt đúng giờ và mang theo thẻ nhân sự hoặc thẻ sinh viên.</p><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Việc cần làm</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Mở chuyến tiếp khách trong hệ thống để xem lịch trình chi tiết và điểm tập trung.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Liên kết yêu cầu đăng nhập hệ thống PEMS.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Reminder: supporting the visit of {{delegationName}}', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">This is a reminder for the visit you agreed to support.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Delegation</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Starts</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedStart}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Ends</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedEnd}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{campusName}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Please arrive on time and bring your staff or student card.</p><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">What to do</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Open the visit in the system to see the detailed agenda and the meeting point.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The link requires signing in to PEMS.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'recipientName,delegationName,campusName,plannedStart,plannedEnd,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 2, '2026-08-08 17:06:15', NULL, '2026-08-21 13:19:07', NULL),
	(70023, 'LOGISTICS_REQUEST_TO_DEPARTMENT', 'Gửi yêu cầu hậu cần tới phòng ban', 'LOGISTICS', NULL, 'Gửi Trưởng phòng ban khi người phụ trách tiếp đón tạo một yêu cầu hậu cần. Có liên kết đồng ý/từ chối dùng một lần.', 'ACTIVE', '[PEMS] Yêu cầu hậu cần: {{logisticsTitle}}', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{departmentLeaderName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">{{requesterName}} gửi tới phòng ban của bạn một yêu cầu hậu cần phục vụ công tác tiếp khách.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Hạng mục</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{logisticsTitle}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Loại</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{logisticsItemType}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Số lượng</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{quantity}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Bắt đầu sử dụng</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{usageStartAt}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Kết thúc sử dụng</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{usageEndAt}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65"><strong>Nội dung chi tiết công việc:</strong> {{logisticsDescription}}</p><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Cần bạn phản hồi</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Vui lòng chọn một phương án bên dưới để chúng tôi tiếp tục xử lý. Nếu cần điều chỉnh số lượng hoặc thời gian, hãy chọn thao tác trong hệ thống để gửi đề xuất thay đổi.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Đồng ý / Từ chối là thao tác trực tiếp, không yêu cầu đăng nhập. Hành động khác (như gán nhân sự, thảo luận thêm) yêu cầu đăng nhập hệ thống. Liên kết phản hồi trực tiếp sẽ hết hạn sau 14 ngày và chỉ sử dụng được một lần.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Logistics request: {{logisticsTitle}}', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{departmentLeaderName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">{{requesterName}} has sent your department a logistics request for a campus visit.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Item</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{logisticsTitle}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Type</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{logisticsItemType}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Quantity</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{quantity}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Use from</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{usageStartAt}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Use until</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{usageEndAt}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65"><strong>Description:</strong> {{logisticsDescription}}</p><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Your response is needed</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Please choose one of the options below so we can proceed. If the quantity or the timing needs to change, use the in-system action to send a counter-proposal.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Accept / Decline are direct actions and do not require signing in. Other action (such as assigning staff or further discussion) requires signing in to the system. The direct response link expires in 14 days and can be used once.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'departmentLeaderName,requesterName,logisticsTitle,logisticsItemType,quantity,usageStartAt,usageEndAt,logisticsDescription,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 2, '2026-08-08 17:06:15', NULL, '2026-08-21 13:19:06', NULL),
	(70024, 'LOGISTICS_ASSIGNEE_ASSIGNMENT', 'Phân công nhân sự xử lý hậu cần', 'LOGISTICS', NULL, 'Gửi nhân sự phòng ban khi Trưởng phòng ban phân công họ xử lý một hạng mục hậu cần. Có liên kết nhận/từ chối dùng một lần.', 'ACTIVE', '[PEMS] Bạn được giao hạng mục hậu cần: {{logisticsTitle}}', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{assigneeName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Trưởng phòng đã giao cho bạn một hạng mục hậu cần phục vụ công tác tiếp khách.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Hạng mục</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{logisticsTitle}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Hạn xử lý</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{dueAt}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Đoàn khách</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{campusName}}</td></tr></tbody></table><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Cần bạn phản hồi</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Vui lòng chọn một phương án bên dưới để phòng ban biết bạn có nhận hạng mục này hay không.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Thao tác Xem chi tiết / Đề xuất thay đổi yêu cầu đăng nhập hệ thống. Liên kết phản hồi trực tiếp sẽ hết hạn sau 14 ngày và chỉ sử dụng được một lần.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Logistics task assigned to you: {{logisticsTitle}}', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{assigneeName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Your department head has assigned you a logistics item for a campus visit.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Item</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{logisticsTitle}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Due</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{dueAt}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Delegation</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{campusName}}</td></tr></tbody></table><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Your response is needed</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Please choose one of the options below so your department knows whether you are taking this on.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Viewing details or proposing a change requires signing in to the system. The direct response link expires in 14 days and can be used once.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'assigneeName,logisticsTitle,dueAt,campusName,delegationName,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 2, '2026-08-08 17:06:15', NULL, '2026-08-21 13:19:06', NULL),
	(70025, 'LOGISTICS_CHANGE_PROPOSAL_TO_HOST', 'Đề xuất thay đổi yêu cầu hậu cần gửi người phụ trách', 'LOGISTICS', NULL, 'Gửi người phụ trách tiếp đón khi phòng ban đề xuất thay đổi một yêu cầu hậu cần. Có liên kết chấp nhận/từ chối dùng một lần.', 'ACTIVE', '[PEMS] Đề xuất thay đổi hạng mục hậu cần: {{logisticsTitle}}', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{hostName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Phòng {{departmentName}} đề xuất điều chỉnh một hạng mục hậu cần của đoàn {{delegationName}}.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Hạng mục</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{logisticsTitle}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Số lượng ban đầu</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{originalQuantity}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Số lượng đề xuất</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{proposedQuantity}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Bắt đầu sử dụng đề xuất</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{proposedUsageStartAt}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Kết thúc sử dụng đề xuất</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{proposedUsageEndAt}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65"><strong>Mô tả đề xuất:</strong> {{proposedDescription}}</p><p style="margin:0 0 14px;color:#334155;line-height:1.65"><strong>Lý do đề xuất:</strong> {{proposalNote}}</p><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Cần bạn phản hồi</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Vui lòng chọn một phương án bên dưới. Nếu bạn chấp nhận, hạng mục sẽ được cập nhật theo nội dung đề xuất.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Đăng nhập để xem chi tiết đề xuất và quyết định Chấp nhận / Từ chối trong hệ thống.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Change proposed for logistics item: {{logisticsTitle}}', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{hostName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">{{departmentName}} proposes a change to a logistics item for {{delegationName}}.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Item</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{logisticsTitle}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Original quantity</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{originalQuantity}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Proposed quantity</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{proposedQuantity}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Proposed use from</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{proposedUsageStartAt}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Proposed use until</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{proposedUsageEndAt}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65"><strong>Proposed description:</strong> {{proposedDescription}}</p><p style="margin:0 0 14px;color:#334155;line-height:1.65"><strong>Reason:</strong> {{proposalNote}}</p><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Your response is needed</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Please choose one of the options below. If you accept, the item will be updated to match the proposal.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Sign in to review the proposal in detail and decide to Accept / Reject it in the system.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'hostName,logisticsTitle,departmentName,delegationName,originalQuantity,proposedQuantity,proposedUsageStartAt,proposedUsageEndAt,proposedDescription,proposalNote,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 2, '2026-08-08 17:06:15', NULL, '2026-08-21 13:19:06', NULL),
	(70026, 'LOGISTICS_EXPENSE_REPORT_REMINDER', 'Nhắc kê khai chi phí hậu cần', 'LOGISTICS', NULL, 'Nhắc người phụ trách hạng mục hoàn tất kê khai chi phí sau chuyến tiếp khách.', 'ACTIVE', '[PEMS] Nhắc kê khai chi phí hạng mục: {{itemTitle}}', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Một hạng mục hậu cần bạn phụ trách đã hoàn tất nhưng chưa có kê khai chi phí.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Hạng mục</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{itemTitle}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Hạn kê khai</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{dueAt}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Đoàn khách</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{delegationName}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Kê khai chi phí là căn cứ để phòng ban tổng hợp quyết toán cho đoàn. Nếu hạng mục không phát sinh chi phí, vui lòng ghi nhận rõ điều đó.</p><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">Việc cần làm</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Mở biên bản trong hệ thống để kê khai chi phí thực tế của hạng mục này.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Sau khi đăng nhập, bạn có thể nhập chi phí hoặc xác nhận "Không có chi phí" cho hạng mục này.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Reminder: record the expenses for {{itemTitle}}', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">A logistics item you are responsible for is complete but has no expense record yet.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Item</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{itemTitle}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Due</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{dueAt}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Delegation</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{delegationName}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">The expense record is what the department uses to settle the visit. If the item cost nothing, please record that explicitly.</p><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 12px;font-weight:700;color:#0f3d67">What to do</p><p style="margin:0 0 14px;color:#334155;line-height:1.6">Open the record in the system and enter the actual expenses for this item.</p>{{actionBlock}}</div><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">After signing in, you can enter the expenses or confirm "No cost" for this item.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'recipientName,itemTitle,dueAt,delegationName,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 4, '2026-08-08 17:06:15', NULL, '2026-08-25 01:22:34', 2),
	(70027, 'REPORT_CAMPUS_OPERATION', 'Báo cáo vận hành campus', 'REPORT', NULL, 'Gửi báo cáo vận hành tiếp khách của một campus kèm tệp PDF đính kèm.', 'ACTIVE', '[PEMS] Báo cáo vận hành cơ sở {{campusName}} kỳ {{periodFrom}} - {{periodTo}}', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Báo cáo vận hành theo cơ sở đã được lập và gửi kèm email này.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Báo cáo</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">Vận hành theo cơ sở</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Kỳ báo cáo</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{periodFrom}} - {{periodTo}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Tệp đính kèm gồm số lượng đoàn đã tiếp đón, tiến độ chuẩn bị và các hạng mục hậu cần trong kỳ.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Số liệu được kết xuất tại thời điểm gửi email. Nếu dữ liệu trong kỳ được cập nhật sau đó, vui lòng kết xuất lại từ hệ thống.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Campus operation report for {{campusName}}, {{periodFrom}} - {{periodTo}}', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">The campus operation report has been generated and is attached to this email.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Report</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">Campus operation</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Period</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{periodFrom}} - {{periodTo}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">The attachment covers the delegations received, preparation progress and logistics items for the period.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The figures were exported when this email was sent. If the data changes afterwards, please export again from the system.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'recipientName,campusName,periodFrom,periodTo,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70028, 'REPORT_DEPARTMENT_COLLABORATION', 'Báo cáo phối hợp tiếp khách của phòng ban', 'REPORT', NULL, 'Gửi Trưởng phòng ban báo cáo mức độ phối hợp tiếp khách của phòng ban kèm tệp PDF đính kèm.', 'ACTIVE', '[PEMS] Báo cáo phối hợp phòng {{departmentName}} kỳ {{periodFrom}} - {{periodTo}}', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Báo cáo phối hợp theo phòng ban đã được lập và gửi kèm email này.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Báo cáo</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">Phối hợp theo phòng ban</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Phòng ban</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{departmentName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Kỳ báo cáo</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{periodFrom}} - {{periodTo}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Tệp đính kèm gồm các yêu cầu hậu cần phòng ban đã tiếp nhận, tỷ lệ chấp nhận và tiến độ hoàn thành trong kỳ.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Số liệu được kết xuất tại thời điểm gửi email. Nếu dữ liệu trong kỳ được cập nhật sau đó, vui lòng kết xuất lại từ hệ thống.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Department collaboration report for {{departmentName}}, {{periodFrom}} - {{periodTo}}', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">The department collaboration report has been generated and is attached to this email.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Report</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">Department collaboration</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Department</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{departmentName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Period</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{periodFrom}} - {{periodTo}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">The attachment covers the logistics requests the department received, the acceptance rate and completion progress for the period.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The figures were exported when this email was sent. If the data changes afterwards, please export again from the system.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'recipientName,departmentName,periodFrom,periodTo,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70029, 'REPORT_DEPARTMENT_INVOICE', 'Hóa đơn hậu cần tiếp khách của phòng ban', 'REPORT', NULL, 'Gửi hóa đơn hậu cần tiếp khách của phòng ban kèm tệp PDF. Dùng chung cho hai chiều gửi: phòng ban gửi lên và campus gửi xuống.', 'ACTIVE', '[PEMS] Bảng kê chi phí phòng {{departmentName}} kỳ {{periodFrom}} - {{periodTo}}', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Bảng kê chi phí theo phòng ban đã được lập và gửi kèm email này.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Báo cáo</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">Bảng kê chi phí</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Phòng ban</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{departmentName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Kỳ báo cáo</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{periodFrom}} - {{periodTo}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Tệp đính kèm liệt kê từng hạng mục hậu cần đã kê khai chi phí trong kỳ, kèm tổng cộng.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Bảng kê chỉ gồm các hạng mục đã được kê khai. Hạng mục chưa kê khai sẽ không xuất hiện cho tới khi người phụ trách hoàn tất.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Expense statement for {{departmentName}}, {{periodFrom}} - {{periodTo}}', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">The department expense statement has been generated and is attached to this email.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Report</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">Expense statement</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Department</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{departmentName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Period</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{periodFrom}} - {{periodTo}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">The attachment lists every logistics item with a recorded expense in the period, with a total.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Only items with a recorded expense appear. An item stays off the statement until whoever is responsible records it.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'recipientName,departmentName,periodFrom,periodTo,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70030, 'REPORT_PERSONNEL_PERFORMANCE', 'Báo cáo hiệu suất nhân sự tiếp khách', 'REPORT', NULL, 'Gửi từng cá nhân báo cáo hiệu suất tham gia tiếp khách kèm tệp PDF. Phạm vi thống kê do người gửi truyền vào qua scopeLabel.', 'ACTIVE', '[PEMS] Báo cáo nhân sự {{scopeLabel}} - {{personName}} kỳ {{periodFrom}} - {{periodTo}}', '<p style="margin:0 0 16px;color:#334155">Xin chào <strong>{{personName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Báo cáo hoạt động nhân sự đã được lập và gửi kèm email này.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Báo cáo</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">Hoạt động nhân sự</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Phạm vi</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{scopeLabel}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Kỳ báo cáo</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{periodFrom}} - {{periodTo}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">Tệp đính kèm gồm số chuyến tiếp khách đã tham gia, nhiệm vụ được giao và tình trạng hoàn thành trong kỳ.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Số liệu được kết xuất tại thời điểm gửi email. Nếu dữ liệu trong kỳ được cập nhật sau đó, vui lòng kết xuất lại từ hệ thống.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Personnel report {{scopeLabel}} - {{personName}}, {{periodFrom}} - {{periodTo}}', '<p style="margin:0 0 16px;color:#334155">Hello <strong>{{personName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">The personnel activity report has been generated and is attached to this email.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Report</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">Personnel activity</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Scope</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{scopeLabel}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Period</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{periodFrom}} - {{periodTo}}</td></tr></tbody></table><p style="margin:0 0 14px;color:#334155;line-height:1.65">The attachment covers the visits taken part in, the tasks assigned and their completion status for the period.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The figures were exported when this email was sent. If the data changes afterwards, please export again from the system.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'personName,scopeLabel,periodFrom,periodTo,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70031, 'VISIT_SETUP_PROGRESS_UPDATE', 'Cập nhật công tác chuẩn bị tiếp khách', 'REPORT', NULL, 'Người phụ trách tiếp đón gửi bản cập nhật công tác chuẩn bị tới khách và thành phần tham gia, kèm Báo cáo Lịch trình. Không mang liên kết dùng một lần.', 'ACTIVE', '[PEMS] Cập nhật công tác chuẩn bị đón đoàn {{delegationName}}', '<p style="margin:0 0 16px;color:#334155">Kính gửi Quý vị,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Chúng tôi xin gửi tới Quý vị thông tin cập nhật về công tác chuẩn bị đón đoàn {{delegationName}} tại {{campusName}}.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Đoàn khách</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Bắt đầu</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedStart}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Kết thúc</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedEnd}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Đầu mối phía FPT</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{hostName}}</td></tr></tbody></table>{{setupSummaryBlock}}<p style="margin:0 0 14px;color:#334155;line-height:1.65">Nếu có nội dung nào cần điều chỉnh, xin Quý vị phản hồi trực tiếp email này để chúng tôi cập nhật trước ngày diễn ra.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">Các thông tin trên được tổng hợp từ dữ liệu chuẩn bị mới nhất tại thời điểm gửi email này.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] Preparation update for the visit of {{delegationName}}', '<p style="margin:0 0 16px;color:#334155">Dear Sir or Madam,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Here is an update on the preparations for the visit of {{delegationName}} to {{campusName}}.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Delegation</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Starts</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedStart}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Ends</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{plannedEnd}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">FPT contact</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{hostName}}</td></tr></tbody></table>{{setupSummaryBlock}}<p style="margin:0 0 14px;color:#334155;line-height:1.65">If anything needs to change, please reply to this email so we can update it before the day.</p><p style="margin:0 0 12px;color:#64748b;font-size:12px;line-height:1.6">The information above reflects the latest preparation data at the time this email was sent.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'delegationName,campusName,plannedStart,plannedEnd,hostName,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70032, 'VISIT_CAMPUS_REJECTED', 'Thông báo cơ sở từ chối tiếp nhận', 'VISIT_REQUEST', NULL, 'Gửi người đăng ký khi một cơ sở từ chối tiếp nhận đoàn. Phạm vi đúng một cơ sở: các cơ sở khác của cùng yêu cầu không bị ảnh hưởng.', 'ACTIVE', '[PEMS] Cơ sở {{campusName}} chưa thể tiếp nhận yêu cầu {{requestCode}}', '<p style="margin:0 0 16px;color:#334155">Kính gửi <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Rất tiếc, cơ sở <strong>{{campusName}}</strong> chưa thể tiếp nhận đoàn của Quý vị trong yêu cầu dưới đây. Các cơ sở khác trong cùng yêu cầu (nếu có) không bị ảnh hưởng bởi quyết định này.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Mã yêu cầu</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{requestCode}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Đoàn</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Thời gian dự kiến</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{plannedTime}}</td></tr></tbody></table><div style="margin:20px 0;padding:16px 18px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px"><p style="margin:0 0 8px;font-weight:700;color:#9a3412">Lý do từ chối</p><p style="margin:0;color:#334155;line-height:1.6">{{reason}}</p></div><p style="margin:0 0 14px;color:#334155;line-height:1.65">Quý vị có thể chỉnh sửa và gửi lại yêu cầu trong trang quản lý yêu cầu tham quan. Lưu ý lịch gửi lại cần cách thời điểm gửi ít nhất 72 giờ.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] {{campusName}} cannot accept request {{requestCode}}', '<p style="margin:0 0 16px;color:#334155">Dear <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">We are sorry to say that <strong>{{campusName}}</strong> cannot host your delegation for the request below. Any other campuses in the same request are unaffected by this decision.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Request code</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{requestCode}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Delegation</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Planned time</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{plannedTime}}</td></tr></tbody></table><div style="margin:20px 0;padding:16px 18px;background:#fff7ed;border:1px solid #fed7aa;border-radius:8px"><p style="margin:0 0 8px;font-weight:700;color:#9a3412">Reason</p><p style="margin:0;color:#334155;line-height:1.6">{{reason}}</p></div><p style="margin:0 0 14px;color:#334155;line-height:1.65">You can edit and resubmit the request from your visit request page. Please note that a resubmitted schedule must start at least 72 hours after it is sent.</p><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'recipientName,requestCode,delegationName,campusName,plannedTime,reason,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL),
	(70033, 'VISIT_CONTACT_INVITATION_EXPIRED', 'Thông báo lời mời đầu mối đã hết hạn', 'VISIT_REQUEST', NULL, 'Gửi người đăng ký khi lời mời xác nhận đầu mối liên hệ hết hạn mà chưa được phản hồi. Không phải thông báo từ chối đơn.', 'ACTIVE', '[PEMS] Lời mời đầu mối cho yêu cầu {{requestCode}} đã hết hạn', '<p style="margin:0 0 16px;color:#334155">Kính gửi <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">Lời mời xác nhận vai trò đầu mối liên hệ mà hệ thống đã gửi tới địa chỉ <strong>{{pendingContactEmailMasked}}</strong> đã hết hạn mà chưa được phản hồi.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Mã yêu cầu</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{requestCode}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Đoàn</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Cơ sở</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Địa chỉ được mời</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{pendingContactEmailMasked}}</td></tr></tbody></table><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 8px;font-weight:700;color:#0f3d67">Việc cần làm</p><p style="margin:0;color:#334155;line-height:1.6">Trong trang chi tiết yêu cầu, Quý vị có thể gửi lại lời mời cho chính địa chỉ này, hoặc chỉ định một đầu mối khác. Liên kết cũ đã không còn sử dụng được.</p></div><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Thông tin người gửi</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Trân trọng,<br/><strong>PEMS - FPT University</strong></p>', '[PEMS] The contact invitation for request {{requestCode}} has expired', '<p style="margin:0 0 16px;color:#334155">Dear <strong>{{recipientName}}</strong>,</p><p style="margin:0 0 14px;color:#334155;line-height:1.65">The invitation to confirm the contact role, sent to <strong>{{pendingContactEmailMasked}}</strong>, has expired without a reply.</p><table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:collapse;width:100%;margin:18px 0;border:1px solid #dbe4ee;border-radius:8px"><tbody><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Request code</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{requestCode}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Delegation</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{delegationName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%;border-bottom:1px solid #e5e7eb">Campus</td><td style="padding:10px 14px;font-weight:600;color:#334155;border-bottom:1px solid #e5e7eb">{{campusName}}</td></tr><tr><td style="padding:10px 14px;color:#64748b;width:34%">Invited address</td><td style="padding:10px 14px;font-weight:600;color:#334155">{{pendingContactEmailMasked}}</td></tr></tbody></table><div style="margin:20px 0;padding:16px 18px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:8px"><p style="margin:0 0 8px;font-weight:700;color:#0f3d67">What to do next</p><p style="margin:0;color:#334155;line-height:1.6">From the request detail page you can send the invitation again to the same address, or name a different contact. The previous link can no longer be used.</p></div><div style="margin:20px 0 0;padding:14px 16px;background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px"><p style="margin:0 0 8px;font-size:12px;font-weight:700;color:#475569">Sender</p><p style="margin:0;line-height:1.65;color:#334155"><strong>{{senderName}}</strong><br/>{{senderRole}}<br/>{{senderDepartment}}<br/>{{senderEmail}}</p></div><p style="margin:22px 0 0;color:#64748b;font-size:12px;line-height:1.6">Best regards,<br/><strong>PEMS - FPT University</strong></p>', 'HTML', 'recipientName,requestCode,delegationName,campusName,pendingContactEmailMasked,senderName,senderRole,senderEmail,senderPhone,senderDepartment,senderCampus', 1, '2026-08-08 17:06:15', NULL, NULL, NULL);

-- Dumping structure for table pems_db.faq_translations
DROP TABLE IF EXISTS `faq_translations`;
CREATE TABLE IF NOT EXISTS `faq_translations` (
  `faq_translation_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `faq_id` bigint unsigned NOT NULL,
  `language_code` enum('vi','en') COLLATE utf8mb4_unicode_ci NOT NULL,
  `question` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL,
  `answer` text COLLATE utf8mb4_unicode_ci NOT NULL,
  `translation_source` enum('AUTO','MANUAL','LEGACY') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'AUTO',
  `translation_status` enum('PENDING','READY','FAILED','OUTDATED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING',
  `source_hash` char(64) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'SHA-256 của question + answer nguồn VI tại lần dịch gần nhất',
  `translated_at` datetime DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`faq_translation_id`),
  UNIQUE KEY `uq_faq_translations_lang` (`faq_id`,`language_code`),
  KEY `idx_faq_translations_lang_status` (`language_code`,`translation_status`),
  KEY `fk_faq_translations_created_by` (`created_by`),
  KEY `fk_faq_translations_updated_by` (`updated_by`),
  FULLTEXT KEY `ft_faq_translations_search` (`question`,`answer`),
  CONSTRAINT `fk_faq_translations_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_faq_translations_faq` FOREIGN KEY (`faq_id`) REFERENCES `faqs` (`faq_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_faq_translations_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=126 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Nội dung FAQ theo ngôn ngữ. Public read không gọi Translation API.';

-- Dumping data for table pems_db.faq_translations: ~28 rows (approximately)

-- Dumping structure for table pems_db.faqs
DROP TABLE IF EXISTS `faqs`;
CREATE TABLE IF NOT EXISTS `faqs` (
  `faq_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `faq_type` enum('ACCOUNT_ACCESS','VISIT_REQUEST','DELEGATION_MANAGEMENT','LOGISTICS_RESOURCE','DOCUMENT_MEDIA','NOTIFICATION_EMAIL','OTHER') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'OTHER' COMMENT 'Loại FAQ theo nhóm chức năng hệ thống PEMS',
  `question` varchar(500) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Câu hỏi FAQ',
  `answer` text COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Câu trả lời FAQ',
  `display_order` int unsigned NOT NULL DEFAULT '0',
  `status` enum('PUBLISHED','HIDDEN') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'HIDDEN' COMMENT 'PUBLISHED=hiển thị trên trang FAQ, HIDDEN=ẩn khỏi người xem thường nhưng người quản lý vẫn thấy',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`faq_id`),
  KEY `idx_faqs_status_order` (`status`,`display_order`),
  KEY `idx_faqs_type_status` (`faq_type`,`status`),
  FULLTEXT KEY `ft_faqs_search` (`question`,`answer`)
) ENGINE=InnoDB AUTO_INCREMENT=16109 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='FAQ tiếng Việt theo nhóm chức năng hệ thống PEMS';

-- Dumping data for table pems_db.faqs: ~14 rows (approximately)

-- Dumping structure for table pems_db.feedback_rating_items
DROP TABLE IF EXISTS `feedback_rating_items`;
CREATE TABLE IF NOT EXISTS `feedback_rating_items` (
  `feedback_rating_item_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `feedback_id` bigint unsigned NOT NULL,
  `criterion_code` varchar(80) COLLATE utf8mb4_unicode_ci NOT NULL,
  `criterion_label` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `rating` tinyint unsigned NOT NULL,
  `display_order` int unsigned NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`feedback_rating_item_id`),
  UNIQUE KEY `uq_feedback_rating_criterion` (`feedback_id`,`criterion_code`),
  KEY `idx_feedback_rating_feedback` (`feedback_id`),
  CONSTRAINT `fk_feedback_rating_items_feedback` FOREIGN KEY (`feedback_id`) REFERENCES `feedbacks` (`feedback_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `feedback_rating_items_chk_1` CHECK ((`rating` between 1 and 5))
) ENGINE=InnoDB AUTO_INCREMENT=47140 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Normalized per-criterion ratings for a feedback submission.';

-- Dumping data for table pems_db.feedback_rating_items: ~63 rows (approximately)

-- Dumping structure for table pems_db.feedbacks
DROP TABLE IF EXISTS `feedbacks`;
CREATE TABLE IF NOT EXISTS `feedbacks` (
  `feedback_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `visit_request_id` bigint unsigned NOT NULL,
  `visit_instance_id` bigint unsigned DEFAULT NULL,
  `feedback_type` enum('VISITOR_OVERALL','HOST_DELEGATION_OVERALL','HOST_PARTICIPANT','HOST_LOGISTICS') COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'VISITOR_OVERALL=Visitor đánh giá chung chuyến thăm; HOST_DELEGATION_OVERALL=Host đánh giá chung đoàn khách; HOST_PARTICIPANT=Host đánh giá các bên tham gia/hỗ trợ; HOST_LOGISTICS=Host đánh giá bên cho mượn đồ/hậu cần',
  `submitted_by_user_id` bigint unsigned NOT NULL COMMENT 'User gửi feedback; Visitor hoặc Host phải có tài khoản hệ thống',
  `submitter_role` enum('VISITOR','HOST') COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Vai trò người gửi feedback trong rule hiện tại',
  `submitter_context` varchar(120) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '' COMMENT 'Ngữ cảnh vai trò người gửi, ví dụ: Host chính, Khách đại diện',
  `submitter_name_snapshot` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Tên người gửi tại thời điểm gửi feedback',
  `target_type` enum('VISIT_REQUEST','VISIT_INSTANCE','VISIT_PARTICIPANT','GUEST_MEMBER','LOGISTICS_ITEM','LOGISTICS_HANDOVER','USER','DEPARTMENT') COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Loại đối tượng nghiệp vụ được đánh giá. GUEST_MEMBER giữ để tương thích dữ liệu nhưng không dùng trong rule active hiện tại',
  `target_user_id` bigint unsigned DEFAULT NULL COMMENT 'User được đánh giá nếu target là cá nhân; NULL đối với feedback tổng thể của Visitor',
  `target_participant_id` bigint unsigned DEFAULT NULL COMMENT 'Participant nội bộ được Host đánh giá',
  `target_guest_member_id` bigint unsigned DEFAULT NULL COMMENT 'Không dùng trong rule hiện tại; Host đánh giá chung đoàn khách qua VISIT_INSTANCE/VISIT_REQUEST, không đánh giá từng guest member',
  `target_logistics_item_id` bigint unsigned DEFAULT NULL COMMENT 'Hạng mục logistics/resource được Host đánh giá',
  `target_handover_id` bigint unsigned DEFAULT NULL COMMENT 'Lần ký mượn/trả cụ thể được Host đánh giá nếu cần',
  `target_department_id` bigint unsigned DEFAULT NULL COMMENT 'Phòng ban/bên cho mượn đồ được Host đánh giá nếu đánh giá theo đơn vị',
  `target_role` varchar(80) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Snapshot vai trò/nhóm đối tượng hiển thị; không dùng làm rule chính',
  `target_context` varchar(120) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT '' COMMENT 'Ngữ cảnh đối tượng được đánh giá, ví dụ: Đoàn khách, Student support, Teabreak, Phòng họp',
  `target_name_snapshot` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Tên hoặc nhãn đối tượng được đánh giá tại thời điểm gửi feedback',
  `rating` tinyint unsigned NOT NULL COMMENT 'Số sao tổng thể từ 1 đến 5; bắt buộc',
  `comment` text COLLATE utf8mb4_unicode_ci COMMENT 'Nội dung nhận xét dạng text; không bắt buộc',
  `submitted_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`feedback_id`),
  KEY `idx_feedbacks_visit_request` (`visit_request_id`),
  KEY `idx_feedbacks_visit_instance` (`visit_instance_id`),
  KEY `idx_feedbacks_type` (`feedback_type`),
  KEY `idx_feedbacks_submitter` (`submitted_by_user_id`),
  KEY `idx_feedbacks_target_type` (`target_type`),
  KEY `idx_feedbacks_target_user` (`target_user_id`),
  KEY `idx_feedbacks_target_participant` (`target_participant_id`),
  KEY `idx_feedbacks_target_guest_member` (`target_guest_member_id`),
  KEY `idx_feedbacks_target_logistics_item` (`target_logistics_item_id`),
  KEY `idx_feedbacks_target_handover` (`target_handover_id`),
  KEY `idx_feedbacks_target_department` (`target_department_id`),
  KEY `idx_feedbacks_submitter_flow` (`submitter_role`,`feedback_type`),
  KEY `idx_feedbacks_rating` (`rating`),
  KEY `idx_feedbacks_submitted_at` (`submitted_at`),
  CONSTRAINT `fk_feedbacks_submitter` FOREIGN KEY (`submitted_by_user_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_feedbacks_target_department` FOREIGN KEY (`target_department_id`) REFERENCES `departments` (`department_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_feedbacks_target_guest_member` FOREIGN KEY (`target_guest_member_id`) REFERENCES `visit_guest_members` (`guest_member_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_feedbacks_target_handover` FOREIGN KEY (`target_handover_id`) REFERENCES `visit_logistics_item_handovers` (`handover_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_feedbacks_target_logistics_item` FOREIGN KEY (`target_logistics_item_id`) REFERENCES `visit_logistics_items` (`logistics_item_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_feedbacks_target_participant` FOREIGN KEY (`target_participant_id`) REFERENCES `visit_participants` (`participant_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_feedbacks_target_user` FOREIGN KEY (`target_user_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_feedbacks_visit_instance` FOREIGN KEY (`visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_instance_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_feedbacks_visit_request` FOREIGN KEY (`visit_request_id`) REFERENCES `visit_requests` (`visit_request_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `chk_feedbacks_flow` CHECK ((((`feedback_type` = _utf8mb4'VISITOR_OVERALL') and (`submitter_role` = _utf8mb4'VISITOR') and (`target_type` in (_utf8mb4'VISIT_REQUEST',_utf8mb4'VISIT_INSTANCE'))) or ((`feedback_type` = _utf8mb4'HOST_DELEGATION_OVERALL') and (`submitter_role` = _utf8mb4'HOST') and (`target_type` in (_utf8mb4'VISIT_REQUEST',_utf8mb4'VISIT_INSTANCE'))) or ((`feedback_type` = _utf8mb4'HOST_PARTICIPANT') and (`submitter_role` = _utf8mb4'HOST') and (`target_type` in (_utf8mb4'VISIT_PARTICIPANT',_utf8mb4'USER',_utf8mb4'DEPARTMENT'))) or ((`feedback_type` = _utf8mb4'HOST_LOGISTICS') and (`submitter_role` = _utf8mb4'HOST') and (`target_type` in (_utf8mb4'LOGISTICS_ITEM',_utf8mb4'LOGISTICS_HANDOVER',_utf8mb4'DEPARTMENT',_utf8mb4'USER'))))),
  CONSTRAINT `chk_feedbacks_rating` CHECK ((`rating` between 1 and 5))
) ENGINE=InnoDB AUTO_INCREMENT=47057 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Feedback theo rule mới: Visitor đánh giá chung chuyến thăm; Host đánh giá chung đoàn khách, các bên tham gia/hỗ trợ và bên cho mượn đồ/hậu cần. Rating sao bắt buộc 1..5; comment text không bắt buộc.';

-- Dumping data for table pems_db.feedbacks: ~63 rows (approximately)

-- Dumping structure for table pems_db.files
DROP TABLE IF EXISTS `files`;
CREATE TABLE IF NOT EXISTS `files` (
  `file_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `storage_provider` enum('LOCAL','S3','AZURE','GCS','GOOGLE_DRIVE','OTHER') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'LOCAL',
  `bucket_name` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `object_key` varchar(700) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Max 700 chars to keep UNIQUE index safe under utf8mb4',
  `original_filename` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `mime_type` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `file_size` bigint unsigned DEFAULT NULL,
  `checksum_sha256` char(64) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `uploaded_by` bigint unsigned DEFAULT NULL,
  `uploaded_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `external_file_id` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'External provider file id, e.g., Google Drive file id',
  `web_view_url` varchar(700) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Open/view URL from external storage provider',
  `download_url` varchar(700) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Direct download URL when provider allows it',
  `thumbnail_url` varchar(700) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Thumbnail URL for image/video preview',
  `file_purpose` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Technical/business file purpose used by referencing entity',
  PRIMARY KEY (`file_id`),
  UNIQUE KEY `uq_files_object_key` (`object_key`),
  KEY `idx_files_uploaded_by` (`uploaded_by`,`uploaded_at`),
  KEY `idx_files_mime_time` (`mime_type`,`uploaded_at`),
  KEY `idx_files_checksum` (`checksum_sha256`),
  KEY `idx_files_external_file_id` (`external_file_id`),
  KEY `idx_files_purpose_time` (`file_purpose`,`uploaded_at`),
  CONSTRAINT `fk_files_uploaded_by` FOREIGN KEY (`uploaded_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=340 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='File metadata only. Binary file is stored outside DB.';

-- Dumping data for table pems_db.files: ~335 rows (approximately)

-- Dumping structure for table pems_db.gallery_areas
DROP TABLE IF EXISTS `gallery_areas`;
CREATE TABLE IF NOT EXISTS `gallery_areas` (
  `area_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `campus_id` bigint unsigned NOT NULL,
  `area_name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `area_name_en` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Tên tiếng Anh đã dịch và lưu trong DB',
  `area_key` varchar(180) COLLATE utf8mb4_unicode_ci NOT NULL,
  `cover_file_id` bigint unsigned DEFAULT NULL COMMENT 'Ảnh đại diện khu vực/tòa/khu lớn',
  `status` enum('ACTIVE','INACTIVE') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE',
  `display_order` int unsigned NOT NULL DEFAULT '0',
  `translation_source` enum('AUTO','MANUAL') COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `translation_status` enum('PENDING','READY','FAILED','OUTDATED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING',
  `translation_source_hash` char(64) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `translated_at` datetime DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`area_id`),
  UNIQUE KEY `uq_gallery_areas_campus_key` (`campus_id`,`area_key`),
  KEY `idx_gallery_areas_campus_status` (`campus_id`,`status`),
  KEY `idx_gallery_areas_order` (`campus_id`,`display_order`),
  KEY `idx_gallery_areas_cover_file` (`cover_file_id`),
  KEY `fk_gallery_areas_created_by` (`created_by`),
  KEY `fk_gallery_areas_updated_by` (`updated_by`),
  CONSTRAINT `fk_gallery_areas_campus` FOREIGN KEY (`campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_gallery_areas_cover_file` FOREIGN KEY (`cover_file_id`) REFERENCES `files` (`file_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_gallery_areas_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_gallery_areas_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=8 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Gallery master data: khu vực/tòa/khu lớn theo campus';

-- Dumping data for table pems_db.gallery_areas: ~7 rows (approximately)

-- Dumping structure for table pems_db.gallery_item_contents
DROP TABLE IF EXISTS `gallery_item_contents`;
CREATE TABLE IF NOT EXISTS `gallery_item_contents` (
  `gallery_item_id` bigint unsigned NOT NULL,
  `description_vi` text COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Mô tả tiếng Việt, bắt buộc',
  `audio_vi_file_id` bigint unsigned NOT NULL COMMENT 'files.file_id của bản ghi âm tiếng Việt, bắt buộc',
  `description_en` text COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Mô tả tiếng Anh, bắt buộc',
  `audio_en_file_id` bigint unsigned NOT NULL COMMENT 'files.file_id của bản ghi âm tiếng Anh, bắt buộc',
  `translation_source` enum('AUTO','MANUAL') COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `translation_status` enum('PENDING','READY','FAILED','OUTDATED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING',
  `translation_source_hash` char(64) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `translated_at` datetime DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`gallery_item_id`),
  KEY `idx_gallery_item_contents_audio_vi` (`audio_vi_file_id`),
  KEY `idx_gallery_item_contents_audio_en` (`audio_en_file_id`),
  KEY `fk_gallery_item_contents_created_by` (`created_by`),
  KEY `fk_gallery_item_contents_updated_by` (`updated_by`),
  FULLTEXT KEY `ft_gallery_item_contents_descriptions` (`description_vi`,`description_en`),
  CONSTRAINT `fk_gallery_item_contents_audio_en` FOREIGN KEY (`audio_en_file_id`) REFERENCES `files` (`file_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_gallery_item_contents_audio_vi` FOREIGN KEY (`audio_vi_file_id`) REFERENCES `files` (`file_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_gallery_item_contents_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_gallery_item_contents_item` FOREIGN KEY (`gallery_item_id`) REFERENCES `gallery_items` (`gallery_item_id`) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT `fk_gallery_item_contents_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `chk_gallery_item_description_en_not_blank` CHECK ((char_length(trim(`description_en`)) > 0)),
  CONSTRAINT `chk_gallery_item_description_vi_not_blank` CHECK ((char_length(trim(`description_vi`)) > 0))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Nội dung mô tả và bản ghi âm song ngữ của Gallery Item (1:1)';

-- Dumping data for table pems_db.gallery_item_contents: ~15 rows (approximately)

-- Dumping structure for table pems_db.gallery_item_media
DROP TABLE IF EXISTS `gallery_item_media`;
CREATE TABLE IF NOT EXISTS `gallery_item_media` (
  `media_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `gallery_item_id` bigint unsigned NOT NULL,
  `file_id` bigint unsigned NOT NULL,
  `media_type` enum('IMAGE','VIDEO') COLLATE utf8mb4_unicode_ci NOT NULL,
  `thumbnail_file_id` bigint unsigned DEFAULT NULL,
  `caption` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `caption_en` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Caption tiếng Anh đã dịch và lưu trong DB',
  `alt_text` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `alt_text_en` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Alt text tiếng Anh đã dịch và lưu trong DB',
  `translation_source` enum('AUTO','MANUAL') COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `translation_status` enum('PENDING','READY','FAILED','OUTDATED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING',
  `translation_source_hash` char(64) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `translated_at` datetime DEFAULT NULL,
  `is_primary` tinyint(1) NOT NULL DEFAULT '0',
  `display_order` int unsigned NOT NULL DEFAULT '0',
  `taken_at` datetime DEFAULT NULL,
  `status` enum('ACTIVE','HIDDEN') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  `deleted_at` datetime DEFAULT NULL,
  `deleted_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`media_id`),
  UNIQUE KEY `uq_gallery_item_media_file` (`file_id`),
  KEY `idx_gallery_item_media_primary` (`gallery_item_id`,`is_primary`),
  KEY `idx_gallery_item_media_item_order` (`gallery_item_id`,`display_order`),
  KEY `idx_gallery_item_media_type` (`media_type`),
  KEY `idx_gallery_item_media_status` (`status`,`deleted_at`),
  KEY `idx_gallery_item_media_thumbnail` (`thumbnail_file_id`),
  KEY `fk_gallery_item_media_created_by` (`created_by`),
  KEY `fk_gallery_item_media_updated_by` (`updated_by`),
  KEY `fk_gallery_item_media_deleted_by` (`deleted_by`),
  CONSTRAINT `fk_gallery_item_media_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_gallery_item_media_deleted_by` FOREIGN KEY (`deleted_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_gallery_item_media_file` FOREIGN KEY (`file_id`) REFERENCES `files` (`file_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_gallery_item_media_item` FOREIGN KEY (`gallery_item_id`) REFERENCES `gallery_items` (`gallery_item_id`) ON DELETE CASCADE ON UPDATE RESTRICT,
  CONSTRAINT `fk_gallery_item_media_thumbnail` FOREIGN KEY (`thumbnail_file_id`) REFERENCES `files` (`file_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_gallery_item_media_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=38 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Ảnh/video thuộc gallery item, liên kết file metadata dùng chung';

-- Dumping data for table pems_db.gallery_item_media: ~37 rows (approximately)

-- Dumping structure for table pems_db.gallery_items
DROP TABLE IF EXISTS `gallery_items`;
CREATE TABLE IF NOT EXISTS `gallery_items` (
  `gallery_item_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `location_id` bigint unsigned NOT NULL,
  `title` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `title_en` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Tiêu đề tiếng Anh đã dịch và lưu trong DB',
  `item_type` enum('MEDIA','VISIT_DELEGATION') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'MEDIA' COMMENT 'MEDIA=ảnh/video giới thiệu vị trí; VISIT_DELEGATION=ảnh/video đoàn khách',
  `media_kind` enum('IMAGE','VIDEO','MIXED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'IMAGE',
  `status` enum('PUBLISHED','HIDDEN') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PUBLISHED',
  `display_order` int unsigned NOT NULL DEFAULT '0',
  `translation_source` enum('AUTO','MANUAL') COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `translation_status` enum('PENDING','READY','FAILED','OUTDATED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING',
  `translation_source_hash` char(64) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `translated_at` datetime DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  `deleted_at` datetime DEFAULT NULL,
  `deleted_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`gallery_item_id`),
  KEY `idx_gallery_items_location_status` (`location_id`,`status`,`deleted_at`),
  KEY `idx_gallery_items_item_type` (`item_type`,`status`,`deleted_at`),
  KEY `idx_gallery_items_media_kind` (`media_kind`),
  KEY `idx_gallery_items_created_at` (`created_at`),
  KEY `fk_gallery_items_created_by` (`created_by`),
  KEY `fk_gallery_items_updated_by` (`updated_by`),
  KEY `fk_gallery_items_deleted_by` (`deleted_by`),
  FULLTEXT KEY `ft_gallery_items_search` (`title`),
  FULLTEXT KEY `ft_gallery_items_search_en` (`title_en`),
  CONSTRAINT `fk_gallery_items_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_gallery_items_deleted_by` FOREIGN KEY (`deleted_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_gallery_items_location` FOREIGN KEY (`location_id`) REFERENCES `gallery_locations` (`location_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_gallery_items_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=16 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Gallery item/bài media thuộc một vị trí cụ thể';

-- Dumping data for table pems_db.gallery_items: ~15 rows (approximately)

-- Dumping structure for table pems_db.gallery_locations
DROP TABLE IF EXISTS `gallery_locations`;
CREATE TABLE IF NOT EXISTS `gallery_locations` (
  `location_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `area_id` bigint unsigned NOT NULL,
  `location_name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `location_name_en` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Tên tiếng Anh đã dịch và lưu trong DB',
  `location_key` varchar(180) COLLATE utf8mb4_unicode_ci NOT NULL,
  `cover_file_id` bigint unsigned DEFAULT NULL COMMENT 'Ảnh đại diện vị trí cụ thể',
  `status` enum('ACTIVE','INACTIVE') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE',
  `display_order` int unsigned NOT NULL DEFAULT '0',
  `translation_source` enum('AUTO','MANUAL') COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `translation_status` enum('PENDING','READY','FAILED','OUTDATED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING',
  `translation_source_hash` char(64) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `translated_at` datetime DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`location_id`),
  UNIQUE KEY `uq_gallery_locations_area_key` (`area_id`,`location_key`),
  KEY `idx_gallery_locations_area_status` (`area_id`,`status`),
  KEY `idx_gallery_locations_order` (`area_id`,`display_order`),
  KEY `idx_gallery_locations_cover_file` (`cover_file_id`),
  KEY `fk_gallery_locations_created_by` (`created_by`),
  KEY `fk_gallery_locations_updated_by` (`updated_by`),
  CONSTRAINT `fk_gallery_locations_area` FOREIGN KEY (`area_id`) REFERENCES `gallery_areas` (`area_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_gallery_locations_cover_file` FOREIGN KEY (`cover_file_id`) REFERENCES `files` (`file_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_gallery_locations_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_gallery_locations_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=10 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Gallery master data: vị trí cụ thể thuộc khu vực';

-- Dumping data for table pems_db.gallery_locations: ~9 rows (approximately)

-- Dumping structure for table pems_db.login_logs
DROP TABLE IF EXISTS `login_logs`;
CREATE TABLE IF NOT EXISTS `login_logs` (
  `login_log_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `user_id` bigint unsigned DEFAULT NULL,
  `email` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `login_portal` enum('VISITOR','INTERNAL') COLLATE utf8mb4_unicode_ci NOT NULL,
  `selected_campus_id` bigint unsigned DEFAULT NULL,
  `provider_type` enum('LOCAL_PASSWORD','GOOGLE_SSO','FEID') COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `status` enum('SUCCESS','FAILED','BLOCKED') COLLATE utf8mb4_unicode_ci NOT NULL,
  `failure_reason` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `ip_address` varchar(45) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `user_agent` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `session_id` bigint unsigned DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`login_log_id`),
  KEY `idx_login_logs_user_time` (`user_id`,`created_at`),
  KEY `idx_login_logs_email_status_time` (`email`,`status`,`created_at`),
  KEY `idx_login_logs_ip_status_time` (`ip_address`,`status`,`created_at`),
  KEY `idx_login_logs_portal_campus` (`login_portal`,`selected_campus_id`),
  KEY `idx_login_logs_provider_time` (`provider_type`,`created_at`),
  KEY `fk_login_logs_campus` (`selected_campus_id`),
  CONSTRAINT `fk_login_logs_campus` FOREIGN KEY (`selected_campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_login_logs_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=645 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Lịch sử đăng nhập';

-- Dumping data for table pems_db.login_logs: ~452 rows (approximately)

-- Dumping structure for table pems_db.minute_action_items
DROP TABLE IF EXISTS `minute_action_items`;
CREATE TABLE IF NOT EXISTS `minute_action_items` (
  `action_item_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `minutes_id` bigint unsigned NOT NULL,
  `title` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Tên đầu việc',
  `note` text COLLATE utf8mb4_unicode_ci COMMENT 'Ghi chú thêm cho đầu việc',
  `assigned_to_user_id` bigint unsigned DEFAULT NULL COMMENT 'Người phụ trách đầu mục — Host hoặc participant ACCEPTED (IC_SUPPORT/DEPT_SUPPORT/STUDENT) của chuyến thăm',
  `due_date` datetime DEFAULT NULL COMMENT 'Deadline ngày giờ của đầu việc',
  `due_reminder_sent_at` datetime DEFAULT NULL COMMENT 'Thời điểm đã gửi mail/thông báo nhắc hạn cho người phụ trách; NULL = chưa nhắc',
  `status` enum('TODO','IN_PROGRESS','DONE','CANCELLED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'TODO' COMMENT 'TODO=chưa làm, IN_PROGRESS=đang làm, DONE=hoàn thành, CANCELLED=đã hủy/không cần làm nữa',
  `completed_at` datetime DEFAULT NULL COMMENT 'Thời điểm hoàn thành; backend tự set khi status chuyển sang DONE',
  `display_order` int unsigned NOT NULL DEFAULT '1' COMMENT 'Thứ tự hiển thị trong biên bản',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`action_item_id`),
  KEY `idx_action_items_minutes` (`minutes_id`),
  KEY `idx_action_items_status_due` (`status`,`due_date`),
  KEY `idx_action_items_order` (`minutes_id`,`display_order`),
  KEY `idx_action_items_created_by_time` (`created_by`,`created_at`),
  KEY `idx_action_items_assignee` (`assigned_to_user_id`,`status`),
  KEY `fk_action_items_updated_by` (`updated_by`),
  CONSTRAINT `fk_action_items_assignee` FOREIGN KEY (`assigned_to_user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_action_items_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_action_items_minutes` FOREIGN KEY (`minutes_id`) REFERENCES `minutes` (`minutes_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_action_items_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=101 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Các đầu việc sau biên bản. Không gán người phụ trách; chỉ có note, deadline và trạng thái hoàn thành.';

-- Dumping data for table pems_db.minute_action_items: ~12 rows (approximately)

-- Dumping structure for table pems_db.minute_participants
DROP TABLE IF EXISTS `minute_participants`;
CREATE TABLE IF NOT EXISTS `minute_participants` (
  `minute_participant_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `minutes_id` bigint unsigned NOT NULL,
  `user_id` bigint unsigned DEFAULT NULL,
  `guest_member_id` bigint unsigned DEFAULT NULL,
  `source_member_type` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Loại thành viên nguồn khi được đưa vào biên bản: GUEST | EXTERNAL_SUPPORT. NULL = dòng nội bộ (user_id) hoặc dòng nhập tay. Là SNAPSHOT: biên bản đã chốt không đổi theo việc phân loại lại thành viên về sau.',
  `is_operational_contact` tinyint(1) NOT NULL DEFAULT '0' COMMENT '1 = người này là đầu mối của cơ sở. Là vai trò BỔ SUNG: một dòng vừa là GUEST vừa là đầu mối, hiển thị "Khách · Đầu mối". Không thay source_member_type.',
  `sync_state` enum('ACTIVE','EXCLUDED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE' COMMENT 'ACTIVE = đang trong biên bản. EXCLUDED = đã loại khỏi biên bản nhưng GIỮ dòng, để "Đồng bộ người mới" không thêm lại và để khôi phục. Dòng nhập tay vẫn được xoá hẳn.',
  `full_name_snapshot` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `role_snapshot` varchar(120) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `organization_snapshot` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `email_snapshot` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `attendance_status` enum('PRESENT','ABSENT','EXCUSED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PRESENT' COMMENT 'PRESENT=có mặt, ABSENT=vắng mặt, EXCUSED=vắng có lý do',
  `attendance_note` text COLLATE utf8mb4_unicode_ci COMMENT 'Ghi chú điểm danh/lý do vắng nếu có',
  `checked_at` datetime DEFAULT NULL COMMENT 'Thời điểm ghi nhận điểm danh',
  `checked_by` bigint unsigned DEFAULT NULL COMMENT 'Người thực hiện điểm danh',
  `display_order` int unsigned NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`minute_participant_id`),
  UNIQUE KEY `uq_minute_participants_minute_guest` (`minutes_id`,`guest_member_id`),
  KEY `idx_minute_participants_minutes_order` (`minutes_id`,`display_order`),
  KEY `idx_minute_participants_user` (`user_id`),
  KEY `idx_minute_participants_guest_member` (`guest_member_id`),
  KEY `idx_minute_participants_attendance` (`minutes_id`,`attendance_status`),
  KEY `idx_minute_participants_checked_by` (`checked_by`),
  KEY `idx_minute_participants_sync_state` (`minutes_id`,`sync_state`),
  CONSTRAINT `fk_minute_participants_checked_by` FOREIGN KEY (`checked_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_minute_participants_guest_member` FOREIGN KEY (`guest_member_id`) REFERENCES `visit_guest_members` (`guest_member_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_minute_participants_minutes` FOREIGN KEY (`minutes_id`) REFERENCES `minutes` (`minutes_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_minute_participants_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=11206 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Snapshot participant list for meeting minutes; replaces minutes.participants_json.';

-- Dumping data for table pems_db.minute_participants: ~150 rows (approximately)

-- Dumping structure for table pems_db.minutes
DROP TABLE IF EXISTS `minutes`;
CREATE TABLE IF NOT EXISTS `minutes` (
  `minutes_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `visit_instance_id` bigint unsigned NOT NULL,
  `title` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `content` longtext COLLATE utf8mb4_unicode_ci,
  `status` enum('DRAFT','SAVED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'DRAFT' COMMENT 'DRAFT=biên bản nháp, SAVED=đã lưu nội dung; quyền sửa bị khóa khi visit instance CLOSED',
  `edit_locked_by` bigint unsigned DEFAULT NULL COMMENT 'User hiện đang giữ quyền sửa biên bản',
  `edit_locked_at` datetime DEFAULT NULL COMMENT 'Thời điểm bắt đầu giữ quyền sửa',
  `edit_lock_expires_at` datetime DEFAULT NULL COMMENT 'Thời điểm lock sửa hết hạn',
  `edit_lock_token` char(36) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Token phiên sửa, dùng để xác nhận đúng người đang giữ lock',
  `row_version` int unsigned NOT NULL DEFAULT '0' COMMENT 'Version chống ghi đè khi cập nhật đồng thời',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`minutes_id`),
  UNIQUE KEY `uq_minutes_visit_instance` (`visit_instance_id`),
  KEY `idx_minutes_visit_status` (`visit_instance_id`,`status`),
  KEY `idx_minutes_created_by_time` (`created_by`,`created_at`),
  KEY `idx_minutes_edit_lock` (`edit_locked_by`,`edit_lock_expires_at`),
  KEY `fk_minutes_updated_by` (`updated_by`),
  FULLTEXT KEY `ft_minutes_search` (`title`,`content`),
  CONSTRAINT `fk_minutes_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_minutes_edit_locked_by` FOREIGN KEY (`edit_locked_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_minutes_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_minutes_visit_instance` FOREIGN KEY (`visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_instance_id`) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=49022 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Biên bản chuyến thăm. Không lưu file đính kèm và không lưu action item dạng JSON; action item tách bảng riêng.';

-- Dumping data for table pems_db.minutes: ~25 rows (approximately)

-- Dumping structure for table pems_db.news
DROP TABLE IF EXISTS `news`;
CREATE TABLE IF NOT EXISTS `news` (
  `news_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `campus_id` bigint unsigned DEFAULT NULL COMMENT 'Campus liên quan đến bài viết. NULL nếu bài toàn hệ thống',
  `visit_instance_id` bigint unsigned DEFAULT NULL COMMENT 'Visit instance liên quan nếu bài viết được tạo từ một chuyến tiếp đón',
  `author_user_id` bigint unsigned NOT NULL COMMENT 'Người tạo/viết bài',
  `cover_file_id` bigint unsigned DEFAULT NULL COMMENT 'Ảnh bìa bài viết, trỏ tới files.file_id',
  `status` enum('PENDING_REVIEW','REJECTED','PUBLISHED','HIDDEN') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING_REVIEW' COMMENT 'PENDING_REVIEW=chờ host duyệt, REJECTED=bị từ chối, PUBLISHED=đã đăng, HIDDEN=ẩn khỏi trang tin',
  `submitted_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Thời điểm người viết gửi bài cho host duyệt',
  `reviewed_by` bigint unsigned DEFAULT NULL COMMENT 'Host duyệt hoặc từ chối bài viết',
  `reviewed_at` datetime DEFAULT NULL COMMENT 'Thời điểm host duyệt hoặc từ chối',
  `review_note` text COLLATE utf8mb4_unicode_ci COMMENT 'Ghi chú duyệt hoặc lý do từ chối',
  `published_at` datetime DEFAULT NULL COMMENT 'Thời điểm bài viết được đăng',
  `is_featured` tinyint(1) NOT NULL DEFAULT '0' COMMENT 'Bài viết nổi bật',
  `is_pinned` tinyint(1) NOT NULL DEFAULT '0' COMMENT 'Bài viết được ghim ở Dấu ấn các chuyến thăm',
  `row_version` int unsigned NOT NULL DEFAULT '0' COMMENT 'Optimistic concurrency token, chống ghi đè khi cập nhật đồng thời',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`news_id`),
  KEY `idx_news_public` (`status`,`campus_id`,`published_at`),
  KEY `idx_news_author_status` (`author_user_id`,`status`),
  KEY `idx_news_visit_instance_status` (`visit_instance_id`,`status`),
  KEY `idx_news_review` (`reviewed_by`,`reviewed_at`),
  KEY `idx_news_featured` (`is_featured`,`status`,`published_at`),
  KEY `idx_news_pinned` (`is_pinned`,`status`,`published_at`),
  KEY `fk_news_campus` (`campus_id`),
  KEY `fk_news_cover_file` (`cover_file_id`),
  CONSTRAINT `fk_news_author` FOREIGN KEY (`author_user_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_news_campus` FOREIGN KEY (`campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_news_cover_file` FOREIGN KEY (`cover_file_id`) REFERENCES `files` (`file_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_news_reviewed_by` FOREIGN KEY (`reviewed_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_news_visit_instance` FOREIGN KEY (`visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_instance_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=17034 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='News metadata. Người tham gia gửi bài, host duyệt/từ chối; nội dung chia theo section.';

-- Dumping data for table pems_db.news: ~24 rows (approximately)

-- Dumping structure for table pems_db.news_content_sections
DROP TABLE IF EXISTS `news_content_sections`;
CREATE TABLE IF NOT EXISTS `news_content_sections` (
  `section_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `news_translation_id` bigint unsigned NOT NULL,
  `section_order` tinyint unsigned NOT NULL COMMENT 'Thứ tự section, từ 1 đến 10',
  `section_title` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Tiêu đề section',
  `section_body_html` longtext COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Nội dung rich text dạng HTML đã sanitize, có thể chứa paragraph, bold, italic, color, link, image',
  `section_body_text` text COLLATE utf8mb4_unicode_ci COMMENT 'Plain text tách từ HTML để search hoặc preview',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`section_id`),
  UNIQUE KEY `uq_news_section_order` (`news_translation_id`,`section_order`),
  KEY `idx_news_sections_translation` (`news_translation_id`),
  FULLTEXT KEY `ft_news_sections_search` (`section_title`,`section_body_text`),
  CONSTRAINT `fk_news_sections_translation` FOREIGN KEY (`news_translation_id`) REFERENCES `news_translations` (`news_translation_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `news_content_sections_chk_1` CHECK ((`section_order` between 1 and 10))
) ENGINE=InnoDB AUTO_INCREMENT=171134 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Các khối nội dung chi tiết của bài viết, tối đa 10 section mỗi bản dịch';

-- Dumping data for table pems_db.news_content_sections: ~76 rows (approximately)

-- Dumping structure for table pems_db.news_section_files
DROP TABLE IF EXISTS `news_section_files`;
CREATE TABLE IF NOT EXISTS `news_section_files` (
  `section_file_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `section_id` bigint unsigned NOT NULL,
  `file_id` bigint unsigned NOT NULL,
  `usage_type` enum('INLINE_IMAGE','ATTACHMENT') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'INLINE_IMAGE' COMMENT 'INLINE_IMAGE=ảnh chèn trong nội dung, ATTACHMENT=file đính kèm',
  `display_order` int unsigned NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`section_file_id`),
  UNIQUE KEY `uq_news_section_file` (`section_id`,`file_id`),
  KEY `idx_news_section_files_section` (`section_id`),
  KEY `idx_news_section_files_file` (`file_id`),
  CONSTRAINT `fk_news_section_files_file` FOREIGN KEY (`file_id`) REFERENCES `files` (`file_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_news_section_files_section` FOREIGN KEY (`section_id`) REFERENCES `news_content_sections` (`section_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=173 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='File/ảnh được dùng trong từng section của bài news';

-- Dumping data for table pems_db.news_section_files: ~164 rows (approximately)

-- Dumping structure for table pems_db.news_translations
DROP TABLE IF EXISTS `news_translations`;
CREATE TABLE IF NOT EXISTS `news_translations` (
  `news_translation_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `news_id` bigint unsigned NOT NULL,
  `language_code` varchar(20) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'vi',
  `title` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Tiêu đề chính của bài viết',
  `slug` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Đường dẫn SEO của bài viết',
  `summary` text COLLATE utf8mb4_unicode_ci COMMENT 'Tóm tắt bài viết',
  `seo_title` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `seo_description` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`news_translation_id`),
  UNIQUE KEY `uq_news_translation_lang` (`news_id`,`language_code`),
  UNIQUE KEY `uq_news_translation_slug_lang` (`slug`,`language_code`),
  KEY `idx_news_translations_lang` (`language_code`),
  FULLTEXT KEY `ft_news_translations_search` (`title`,`summary`),
  CONSTRAINT `fk_news_translations_news` FOREIGN KEY (`news_id`) REFERENCES `news` (`news_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=171068 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Tiêu đề, slug, tóm tắt và SEO của bài viết theo ngôn ngữ';

-- Dumping data for table pems_db.news_translations: ~50 rows (approximately)

-- Dumping structure for table pems_db.notifications
DROP TABLE IF EXISTS `notifications`;
CREATE TABLE IF NOT EXISTS `notifications` (
  `notification_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `recipient_user_id` bigint unsigned NOT NULL,
  `actor_user_id` bigint unsigned DEFAULT NULL COMMENT 'User who caused the notification, if applicable.',
  `title` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `message` text COLLATE utf8mb4_unicode_ci,
  `notification_type` varchar(80) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Specific business event type, for example VISIT_CANCELLED, INVITATION_RECEIVED, HOST_FEEDBACK_RECEIVED.',
  `category` varchar(80) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'GENERAL' COMMENT 'UI grouping: VISIT, INVITATION, REMINDER, FEEDBACK, LOGISTICS, HANDOVER, NEWS, PARTNER, ACCOUNT, SYSTEM, GENERAL.',
  `priority` enum('LOW','NORMAL','HIGH','URGENT') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'NORMAL',
  `is_action_required` tinyint(1) NOT NULL DEFAULT '0' COMMENT 'TRUE when the recipient should take an action, not just read the notification.',
  `related_type` varchar(80) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Backward-compatible generic target type.',
  `related_id` bigint unsigned DEFAULT NULL COMMENT 'Backward-compatible generic target id.',
  `visit_request_id` bigint unsigned DEFAULT NULL COMMENT 'Direct visit request context for filtering and navigation.',
  `visit_instance_id` bigint unsigned DEFAULT NULL COMMENT 'Direct campus visit instance context for filtering and navigation.',
  `campus_id` bigint unsigned DEFAULT NULL COMMENT 'Direct campus context for Staff Leader/HO filtering.',
  `action_type` varchar(80) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Frontend action, for example OPEN_VISIT_DETAIL, OPEN_INVITATION_MODAL, OPEN_HOST_FEEDBACK_MODAL.',
  `action_url` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Frontend route to open when the notification is clicked.',
  `metadata_json` json DEFAULT NULL COMMENT 'Small UI payload for modal/detail rendering; do not store source-of-truth business data here.',
  `dedupe_key` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Idempotency key to prevent duplicate notifications, especially reminders/background jobs.',
  `is_read` tinyint(1) NOT NULL DEFAULT '0',
  `read_at` datetime DEFAULT NULL,
  `archived_at` datetime DEFAULT NULL COMMENT 'Soft archive/hide timestamp for the recipient.',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`notification_id`),
  UNIQUE KEY `uq_notifications_recipient_dedupe` (`recipient_user_id`,`dedupe_key`),
  KEY `idx_notifications_user_read_time` (`recipient_user_id`,`is_read`,`created_at`),
  KEY `idx_notifications_related` (`related_type`,`related_id`),
  KEY `idx_notifications_type_time` (`notification_type`,`created_at`),
  KEY `idx_notifications_user_category_time` (`recipient_user_id`,`category`,`created_at`),
  KEY `idx_notifications_user_action_time` (`recipient_user_id`,`is_action_required`,`is_read`,`created_at`),
  KEY `idx_notifications_actor` (`actor_user_id`,`created_at`),
  KEY `idx_notifications_visit_request` (`visit_request_id`,`created_at`),
  KEY `idx_notifications_visit_instance` (`visit_instance_id`,`created_at`),
  KEY `idx_notifications_campus` (`campus_id`,`created_at`),
  CONSTRAINT `fk_notifications_actor_user` FOREIGN KEY (`actor_user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_notifications_campus` FOREIGN KEY (`campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_notifications_user` FOREIGN KEY (`recipient_user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_notifications_visit_instance` FOREIGN KEY (`visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_instance_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_notifications_visit_request` FOREIGN KEY (`visit_request_id`) REFERENCES `visit_requests` (`visit_request_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=99395 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='In-app notifications. Global notification center table; supports role/context filtering, click actions, modal metadata, reminders and idempotency.';

-- Dumping data for table pems_db.notifications: ~408 rows (approximately)

-- Dumping structure for table pems_db.otp_tokens
DROP TABLE IF EXISTS `otp_tokens`;
CREATE TABLE IF NOT EXISTS `otp_tokens` (
  `otp_token_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `user_id` bigint unsigned DEFAULT NULL,
  `email` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `token_type` enum('OTP_CODE','MAGIC_LINK') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'OTP_CODE',
  `purpose` enum('VISIT_REQUEST_VERIFY','CHANGE_SENSITIVE_ACTION') COLLATE utf8mb4_unicode_ci NOT NULL,
  `token_hash` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `challenge_token_hash` char(64) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'SHA-256 của opaque challenge token; không lưu raw token',
  `submission_id` char(36) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'UUID của submit intent UC17',
  `issue_reason` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'INITIAL' COMMENT 'INITIAL, RESEND hoặc HUMAN_RECOVERY',
  `expires_at` datetime NOT NULL,
  `used_at` datetime DEFAULT NULL,
  `attempt_count` int unsigned NOT NULL DEFAULT '0',
  `last_attempt_at` datetime DEFAULT NULL,
  `next_attempt_allowed_at` datetime DEFAULT NULL COMMENT 'Server-enforced progressive cooldown; verify trước thời điểm này bị 429',
  `human_verification_required_at` datetime DEFAULT NULL COMMENT 'Set khi sai lần thứ max_attempts — challenge chỉ phục hồi qua human verification',
  `human_verified_at` datetime DEFAULT NULL,
  `invalidated_at` datetime DEFAULT NULL,
  `invalidation_reason` varchar(40) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'MAX_ATTEMPTS, HUMAN_RECOVERY, SUPERSEDED_BY_RESEND, ...',
  `max_attempts` int unsigned NOT NULL DEFAULT '10',
  `resend_count` int unsigned NOT NULL DEFAULT '0',
  `ip_address` varchar(45) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `user_agent` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`otp_token_id`),
  UNIQUE KEY `uq_otp_tokens_hash` (`token_hash`),
  UNIQUE KEY `uq_otp_challenge_token_hash` (`challenge_token_hash`),
  KEY `idx_otp_submission` (`submission_id`),
  KEY `idx_otp_email_purpose_time` (`email`,`purpose`,`created_at`),
  KEY `idx_otp_email_purpose_active` (`email`,`purpose`,`used_at`,`expires_at`),
  KEY `idx_otp_email_purpose_active_v2` (`email`,`purpose`,`invalidated_at`,`expires_at`),
  KEY `idx_otp_issue_limit` (`email`,`purpose`,`issue_reason`,`created_at`),
  KEY `idx_otp_user_purpose_active` (`user_id`,`purpose`,`used_at`,`expires_at`),
  KEY `idx_otp_ip_time` (`ip_address`,`created_at`),
  CONSTRAINT `fk_otp_tokens_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=22 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='OTP, magic link, set password token, reset password token';

-- Dumping data for table pems_db.otp_tokens: ~21 rows (approximately)

-- Dumping structure for table pems_db.partner_aliases
DROP TABLE IF EXISTS `partner_aliases`;
CREATE TABLE IF NOT EXISTS `partner_aliases` (
  `partner_alias_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `partner_id` bigint unsigned NOT NULL,
  `alias_name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `alias_name_key` varchar(300) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Normalized key: lower-case, remove accents, punctuation, repeated spaces',
  `source` enum('MANUAL','OCR','AUTO_MATCH','IMPORT') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'MANUAL',
  `status` enum('ACTIVE','INACTIVE') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`partner_alias_id`),
  UNIQUE KEY `uq_partner_alias_key` (`partner_id`,`alias_name_key`),
  KEY `idx_partner_alias_lookup` (`alias_name_key`,`status`),
  KEY `idx_partner_alias_partner` (`partner_id`),
  KEY `fk_partner_alias_created_by` (`created_by`),
  KEY `fk_partner_alias_updated_by` (`updated_by`),
  CONSTRAINT `fk_partner_alias_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_partner_alias_partner` FOREIGN KEY (`partner_id`) REFERENCES `partners` (`partner_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_partner_alias_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=49 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Alternative names of partners for matching organization names from guests/OCR.';

-- Dumping data for table pems_db.partner_aliases: ~48 rows (approximately)

-- Dumping structure for table pems_db.partner_contacts
DROP TABLE IF EXISTS `partner_contacts`;
CREATE TABLE IF NOT EXISTS `partner_contacts` (
  `contact_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `partner_id` bigint unsigned NOT NULL,
  `full_name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `email` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `phone` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `job_title` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `department_name` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `source_type` enum('MANUAL','BUSINESS_CARD_OCR','IMPORT') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'MANUAL',
  `scanned_card_file_id` bigint unsigned DEFAULT NULL,
  `avatar_file_id` bigint unsigned DEFAULT NULL COMMENT 'Optional contact avatar stored in files',
  `ocr_confidence` decimal(5,2) DEFAULT NULL,
  `note` text COLLATE utf8mb4_unicode_ci,
  `is_primary` tinyint(1) NOT NULL DEFAULT '0',
  `status` enum('ACTIVE','INACTIVE') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`contact_id`),
  UNIQUE KEY `uq_partner_contacts_partner_email` (`partner_id`,`email`),
  KEY `idx_partner_contacts_partner` (`partner_id`),
  KEY `idx_partner_contacts_email` (`email`),
  KEY `idx_partner_contacts_status` (`status`),
  KEY `idx_partner_contacts_source_type` (`source_type`),
  KEY `idx_partner_contacts_scanned_card` (`scanned_card_file_id`),
  KEY `idx_partner_contacts_avatar_file` (`avatar_file_id`),
  CONSTRAINT `fk_partner_contacts_avatar_file` FOREIGN KEY (`avatar_file_id`) REFERENCES `files` (`file_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_partner_contacts_partner` FOREIGN KEY (`partner_id`) REFERENCES `partners` (`partner_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_partner_contacts_scanned_card` FOREIGN KEY (`scanned_card_file_id`) REFERENCES `files` (`file_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=108 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Người liên hệ đối tác. OCR final confirmed data saved here.';

-- Dumping data for table pems_db.partner_contacts: ~107 rows (approximately)

-- Dumping structure for table pems_db.partner_translations
DROP TABLE IF EXISTS `partner_translations`;
CREATE TABLE IF NOT EXISTS `partner_translations` (
  `partner_translation_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `partner_id` bigint unsigned NOT NULL,
  `language_code` enum('vi','en') COLLATE utf8mb4_unicode_ci NOT NULL,
  `name` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL,
  `short_name` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `country` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `city` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `description` text COLLATE utf8mb4_unicode_ci,
  `address` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `translation_source` enum('AUTO','MANUAL','LEGACY') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'AUTO',
  `translation_status` enum('PENDING','READY','FAILED','OUTDATED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING',
  `source_hash` char(64) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'SHA-256 của nội dung nguồn VI tại lần dịch gần nhất; tránh gọi API khi nội dung không đổi',
  `translated_at` datetime DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`partner_translation_id`),
  UNIQUE KEY `uq_partner_translations_lang` (`partner_id`,`language_code`),
  KEY `idx_partner_translations_lang_status` (`language_code`,`translation_status`),
  KEY `fk_partner_translations_created_by` (`created_by`),
  KEY `fk_partner_translations_updated_by` (`updated_by`),
  FULLTEXT KEY `ft_partner_translations_search` (`name`,`short_name`,`description`,`address`),
  CONSTRAINT `fk_partner_translations_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_partner_translations_partner` FOREIGN KEY (`partner_id`) REFERENCES `partners` (`partner_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_partner_translations_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=93 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Nội dung Partner theo ngôn ngữ. Public read chỉ đọc DB, Translation API chỉ chạy khi tạo/sửa nội dung.';

-- Dumping data for table pems_db.partner_translations: ~82 rows (approximately)

-- Dumping structure for table pems_db.partners
DROP TABLE IF EXISTS `partners`;
CREATE TABLE IF NOT EXISTS `partners` (
  `partner_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `owner_campus_id` bigint unsigned NOT NULL COMMENT 'Campus sở hữu/quản lý partner request; dùng để Staff Leader duyệt đúng campus',
  `partner_code` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `name` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL,
  `short_name` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `country` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `city` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `website_url` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `partner_type` enum('UNIVERSITY','COMPANY','GOVERNMENT','NGO','OTHER') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'UNIVERSITY',
  `cooperation_status` enum('POTENTIAL','ACTIVE','INACTIVE','BLACKLISTED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'POTENTIAL',
  `description` text COLLATE utf8mb4_unicode_ci,
  `logo_file_id` bigint unsigned DEFAULT NULL COMMENT 'Partner logo file, references files.file_id',
  `cover_file_id` bigint unsigned DEFAULT NULL COMMENT 'Partner cover/banner file, references files.file_id',
  `address` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `public_slug` varchar(180) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Public URL slug for partner profile',
  `profile_status` enum('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'APPROVED',
  `review_note` text COLLATE utf8mb4_unicode_ci,
  `reviewed_by` bigint unsigned DEFAULT NULL,
  `reviewed_at` datetime DEFAULT NULL,
  `visibility` enum('PRIVATE','INTERNAL','PUBLIC') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PUBLIC',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`partner_id`),
  UNIQUE KEY `uq_partners_code` (`partner_code`),
  UNIQUE KEY `uq_partners_public_slug` (`public_slug`),
  KEY `idx_partners_owner_status` (`owner_campus_id`,`profile_status`),
  KEY `idx_partners_owner_created` (`owner_campus_id`,`created_at`),
  KEY `idx_partners_country` (`country`),
  KEY `idx_partners_status` (`cooperation_status`),
  KEY `idx_partners_type_status` (`partner_type`,`cooperation_status`),
  KEY `idx_partners_created_at` (`created_at`),
  KEY `idx_partners_profile_status` (`profile_status`),
  KEY `idx_partners_visibility` (`visibility`),
  KEY `idx_partners_logo_file` (`logo_file_id`),
  KEY `idx_partners_cover_file` (`cover_file_id`),
  KEY `idx_partners_reviewed_by` (`reviewed_by`,`reviewed_at`),
  FULLTEXT KEY `ft_partners_search` (`name`,`short_name`,`description`),
  CONSTRAINT `fk_partners_cover_file` FOREIGN KEY (`cover_file_id`) REFERENCES `files` (`file_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_partners_logo_file` FOREIGN KEY (`logo_file_id`) REFERENCES `files` (`file_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_partners_owner_campus` FOREIGN KEY (`owner_campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_partners_reviewed_by` FOREIGN KEY (`reviewed_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=162 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Hồ sơ đối tác; owner_campus_id dùng để Staff Leader duyệt đúng campus';

-- Dumping data for table pems_db.partners: ~58 rows (approximately)

-- Dumping structure for table pems_db.photo_face_tags
DROP TABLE IF EXISTS `photo_face_tags`;
CREATE TABLE IF NOT EXISTS `photo_face_tags` (
  `face_tag_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `file_id` bigint unsigned NOT NULL,
  `tagged_user_id` bigint unsigned DEFAULT NULL COMMENT 'User nội bộ được tag nếu người này có tài khoản hệ thống',
  `visit_request_id` bigint unsigned DEFAULT NULL,
  `guest_member_id` bigint unsigned DEFAULT NULL,
  `partner_contact_id` bigint unsigned DEFAULT NULL,
  `display_name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Tên hiển thị của người được tag trong ảnh',
  `person_name_key` varchar(180) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Tên đã normalize để tìm kiếm không phân biệt dấu/hoa thường',
  `bounding_box_x` decimal(8,4) DEFAULT NULL,
  `bounding_box_y` decimal(8,4) DEFAULT NULL,
  `bounding_box_width` decimal(8,4) DEFAULT NULL,
  `bounding_box_height` decimal(8,4) DEFAULT NULL,
  `tag_status` enum('ACTIVE','REMOVED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `removed_at` datetime DEFAULT NULL,
  `removed_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`face_tag_id`),
  KEY `idx_face_tags_file` (`file_id`),
  KEY `idx_face_tags_display_name` (`display_name`),
  KEY `idx_face_tags_name_key` (`person_name_key`),
  KEY `idx_face_tags_user` (`tagged_user_id`),
  KEY `idx_face_tags_visit_request` (`visit_request_id`),
  KEY `idx_face_tags_guest` (`guest_member_id`),
  KEY `idx_face_tags_partner_contact` (`partner_contact_id`),
  KEY `idx_face_tags_status` (`tag_status`),
  KEY `fk_face_tags_created_by` (`created_by`),
  KEY `fk_face_tags_removed_by` (`removed_by`),
  CONSTRAINT `fk_face_tags_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_face_tags_file` FOREIGN KEY (`file_id`) REFERENCES `files` (`file_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_face_tags_guest` FOREIGN KEY (`guest_member_id`) REFERENCES `visit_guest_members` (`guest_member_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_face_tags_partner_contact` FOREIGN KEY (`partner_contact_id`) REFERENCES `partner_contacts` (`contact_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_face_tags_removed_by` FOREIGN KEY (`removed_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_face_tags_user` FOREIGN KEY (`tagged_user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_face_tags_visit_request` FOREIGN KEY (`visit_request_id`) REFERENCES `visit_requests` (`visit_request_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=35 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Tag người xuất hiện trong ảnh theo file; dùng để tìm ảnh theo tên, không lưu vector sinh trắc học';

-- Dumping data for table pems_db.photo_face_tags: ~34 rows (approximately)

-- Dumping structure for table pems_db.roles
DROP TABLE IF EXISTS `roles`;
CREATE TABLE IF NOT EXISTS `roles` (
  `role_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `role_code` varchar(30) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'ADMIN, HO, STAFF, DEPARTMENT, STUDENT, VISITOR',
  `name` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `status` enum('ACTIVE','INACTIVE') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`role_id`),
  UNIQUE KEY `uq_roles_code` (`role_code`),
  KEY `idx_roles_status` (`status`),
  CONSTRAINT `roles_chk_1` CHECK ((`role_code` in (_utf8mb4'ADMIN',_utf8mb4'HO',_utf8mb4'STAFF',_utf8mb4'DEPARTMENT',_utf8mb4'STUDENT',_utf8mb4'VISITOR')))
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Fixed system roles for account classification. No DB-backed dynamic permission matrix.';

-- Dumping data for table pems_db.roles: ~6 rows (approximately)
INSERT IGNORE INTO `roles` (`role_id`, `role_code`, `name`, `description`, `status`, `created_at`) VALUES
	(1, 'ADMIN', 'System Administrator', 'Quản trị kỹ thuật: cấu hình hệ thống, API và audit; không phải business super-admin của visit/delegation.', 'ACTIVE', '2026-01-05 08:00:00'),
	(2, 'HO', 'Head Office', 'Head Office theo dõi tiến độ liên cơ sở, xem báo cáo tổng hợp và giám sát tuân thủ ở chế độ chỉ đọc; quyết định tại từng campus thuộc Staff Leader của campus đó.', 'ACTIVE', '2026-01-05 08:05:00'),
	(3, 'STAFF', 'IC Staff', 'Nhóm IC campus, gồm Staff Leader và Staff thường thông qua sub_role.', 'ACTIVE', '2026-01-05 08:10:00'),
	(4, 'DEPARTMENT', 'Department Personnel', 'Nhân sự phòng ban ngoài IC; gồm Department Lead và Department Staff.', 'ACTIVE', '2026-01-05 08:15:00'),
	(5, 'STUDENT', 'Student Support', 'Sinh viên hỗ trợ đoàn theo phân công, không có sub_role và không thuộc department.', 'ACTIVE', '2026-01-05 08:20:00'),
	(6, 'VISITOR', 'External Visitor', 'Khách bên ngoài, sử dụng Visitor Portal và chỉ sở hữu dữ liệu request của chính mình.', 'ACTIVE', '2026-01-05 08:25:00');

-- Dumping structure for table pems_db.security_events
DROP TABLE IF EXISTS `security_events`;
CREATE TABLE IF NOT EXISTS `security_events` (
  `security_event_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `user_id` bigint unsigned DEFAULT NULL,
  `email_snapshot` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Email nhận từ SSO hoặc email đang được kiểm tra tại thời điểm xảy ra sự kiện',
  `event_type` enum('SSO_LOGIN','PORTAL_VALIDATION','CAMPUS_VALIDATION','VISITOR_AUTO_PROVISION','SESSION_CREATED','SESSION_REVOKED','SESSION_EXPIRED','TOKEN_REFRESH','SECURITY_POLICY_CHECK') COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Loại sự kiện bảo mật theo mô hình SSO-only',
  `result` enum('SUCCESS','FAILED','BLOCKED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'SUCCESS' COMMENT 'Kết quả xử lý sự kiện',
  `failure_reason_code` enum('ACCOUNT_NOT_FOUND','ACCOUNT_DISABLED','PORTAL_MISMATCH','CAMPUS_MISMATCH','ROLE_MISMATCH','SSO_PROVIDER_ERROR','INVALID_SSO_CLAIMS','VISITOR_AUTO_PROVISION_DISABLED','SESSION_EXPIRED','TOKEN_REVOKED','SUSPICIOUS_IP','UNKNOWN') COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Mã lý do thất bại/chặn; NULL khi SUCCESS',
  `severity` enum('LOW','MEDIUM','HIGH','CRITICAL') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'LOW',
  `login_portal` enum('VISITOR','INTERNAL') COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Portal được dùng khi phát sinh sự kiện',
  `selected_campus_id` bigint unsigned DEFAULT NULL COMMENT 'Campus người dùng chọn ở Internal Portal; NULL với Visitor Portal',
  `provider_type` enum('GOOGLE_SSO','FEID') COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Nguồn định danh SSO; không dùng LOCAL_PASSWORD trong security_events',
  `ip_address` varchar(45) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `user_agent` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `session_id` bigint unsigned DEFAULT NULL,
  `detail_text` text COLLATE utf8mb4_unicode_ci COMMENT 'Ghi chú debug ngắn, không lưu JSON metadata',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`security_event_id`),
  KEY `idx_security_user_time` (`user_id`,`created_at`),
  KEY `idx_security_email_time` (`email_snapshot`,`created_at`),
  KEY `idx_security_type_result_time` (`event_type`,`result`,`created_at`),
  KEY `idx_security_portal_campus_time` (`login_portal`,`selected_campus_id`,`created_at`),
  KEY `idx_security_failure_reason_time` (`failure_reason_code`,`created_at`),
  KEY `idx_security_ip_time` (`ip_address`,`created_at`),
  KEY `idx_security_severity_time` (`severity`,`created_at`),
  KEY `idx_security_session_time` (`session_id`,`created_at`),
  KEY `fk_security_events_selected_campus` (`selected_campus_id`),
  CONSTRAINT `fk_security_events_selected_campus` FOREIGN KEY (`selected_campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_security_events_session` FOREIGN KEY (`session_id`) REFERENCES `user_sessions` (`session_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_security_events_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=746 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='SSO-only security events: portal/campus validation, Visitor auto-provisioning, and session lifecycle. No local password tracking and no metadata JSON.';

-- Dumping data for table pems_db.security_events: ~459 rows (approximately)

-- Dumping structure for table pems_db.sent_email_attachments
DROP TABLE IF EXISTS `sent_email_attachments`;
CREATE TABLE IF NOT EXISTS `sent_email_attachments` (
  `sent_email_attachment_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `sent_email_id` bigint unsigned NOT NULL,
  `file_id` bigint unsigned NOT NULL,
  `attachment_type` enum('ATTACHMENT','INLINE_IMAGE') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ATTACHMENT' COMMENT 'ATTACHMENT=file đính kèm; INLINE_IMAGE=ảnh hiển thị trong nội dung HTML bằng cid',
  `content_id` varchar(120) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Content-ID dùng cho ảnh inline, ví dụ <img src="cid:pems-image-001">',
  `display_name` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `display_order` int unsigned NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`sent_email_attachment_id`),
  UNIQUE KEY `uq_sent_email_attachments_content` (`sent_email_id`,`content_id`),
  KEY `idx_sent_email_attachments_email` (`sent_email_id`),
  KEY `idx_sent_email_attachments_file` (`file_id`),
  KEY `idx_sent_email_attachments_type` (`sent_email_id`,`attachment_type`),
  CONSTRAINT `fk_sent_email_attachments_email` FOREIGN KEY (`sent_email_id`) REFERENCES `sent_emails` (`sent_email_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_sent_email_attachments_file` FOREIGN KEY (`file_id`) REFERENCES `files` (`file_id`) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=14 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Files/images attached to sent emails; binary content is stored in files/external storage';

-- Dumping data for table pems_db.sent_email_attachments: ~13 rows (approximately)

-- Dumping structure for table pems_db.sent_email_recipients
DROP TABLE IF EXISTS `sent_email_recipients`;
CREATE TABLE IF NOT EXISTS `sent_email_recipients` (
  `sent_email_recipient_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `sent_email_id` bigint unsigned NOT NULL,
  `recipient_email` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `recipient_name` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `recipient_type` enum('TO','CC','BCC') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'TO',
  `delivery_status` enum('QUEUED','SENT','DELIVERED','FAILED','BOUNCED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'QUEUED',
  `provider_message_id` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `error_message` text COLLATE utf8mb4_unicode_ci,
  `sent_at` datetime DEFAULT NULL,
  `delivered_at` datetime DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`sent_email_recipient_id`),
  KEY `idx_sent_email_recipients_sent_email` (`sent_email_id`),
  KEY `idx_sent_email_recipients_email_status` (`recipient_email`,`delivery_status`),
  FULLTEXT KEY `ft_sent_email_recipients_search` (`recipient_email`,`recipient_name`),
  CONSTRAINT `fk_sent_email_recipients_email` FOREIGN KEY (`sent_email_id`) REFERENCES `sent_emails` (`sent_email_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=99272 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='One row per recipient; replaces sent_emails.recipients_json.';

-- Dumping data for table pems_db.sent_email_recipients: ~184 rows (approximately)

-- Dumping structure for table pems_db.sent_emails
DROP TABLE IF EXISTS `sent_emails`;
CREATE TABLE IF NOT EXISTS `sent_emails` (
  `sent_email_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `email_template_id` bigint unsigned DEFAULT NULL,
  `related_type` varchar(80) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `related_id` bigint unsigned DEFAULT NULL,
  `subject` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `body_snapshot` longtext COLLATE utf8mb4_unicode_ci,
  `body_format` enum('PLAIN_TEXT','HTML') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'HTML' COMMENT 'Định dạng body_snapshot sau khi merge template/editor',
  `provider_thread_id` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `provider_message_id` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `retry_count` int unsigned NOT NULL DEFAULT '0',
  `last_attempt_at` datetime DEFAULT NULL,
  `delivered_at` datetime DEFAULT NULL,
  `status` enum('QUEUED','SENT','DELIVERED','FAILED','PARTIAL_FAILED','BOUNCED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'QUEUED',
  `error_message` text COLLATE utf8mb4_unicode_ci,
  `sent_by` bigint unsigned DEFAULT NULL,
  `sent_at` datetime DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`sent_email_id`),
  KEY `idx_sent_emails_template` (`email_template_id`),
  KEY `idx_sent_emails_related` (`related_type`,`related_id`),
  KEY `idx_sent_emails_status_time` (`status`,`created_at`),
  KEY `idx_sent_emails_sent_by_time` (`sent_by`,`sent_at`),
  KEY `idx_sent_emails_provider_thread` (`provider_thread_id`),
  KEY `idx_sent_emails_provider_message` (`provider_message_id`),
  CONSTRAINT `fk_sent_emails_sent_by` FOREIGN KEY (`sent_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_sent_emails_template` FOREIGN KEY (`email_template_id`) REFERENCES `email_templates` (`email_template_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=99149 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Sent email log; recipients stored in sent_email_recipients';

-- Dumping data for table pems_db.sent_emails: ~127 rows (approximately)

-- Dumping structure for table pems_db.user_auth_providers
DROP TABLE IF EXISTS `user_auth_providers`;
CREATE TABLE IF NOT EXISTS `user_auth_providers` (
  `auth_provider_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `user_id` bigint unsigned NOT NULL,
  `provider_type` enum('LOCAL_PASSWORD','GOOGLE_SSO','FEID') COLLATE utf8mb4_unicode_ci NOT NULL,
  `provider_subject` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Required for GOOGLE_SSO/FEID',
  `provider_email` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `is_enabled` tinyint(1) NOT NULL DEFAULT '1',
  `linked_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `last_used_at` datetime DEFAULT NULL,
  PRIMARY KEY (`auth_provider_id`),
  UNIQUE KEY `uq_user_auth_provider_type` (`user_id`,`provider_type`),
  UNIQUE KEY `uq_auth_provider_subject` (`provider_type`,`provider_subject`),
  KEY `idx_auth_provider_email` (`provider_email`),
  KEY `idx_auth_provider_type_email_enabled` (`provider_type`,`provider_email`,`is_enabled`),
  CONSTRAINT `fk_auth_providers_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=277 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Provider đăng nhập của user. Production dùng GOOGLE_SSO/FEID; LOCAL_PASSWORD chỉ dùng DEV/test.';

-- Dumping data for table pems_db.user_auth_providers: ~105 rows (approximately)

-- Dumping structure for table pems_db.user_sessions
DROP TABLE IF EXISTS `user_sessions`;
CREATE TABLE IF NOT EXISTS `user_sessions` (
  `session_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `user_id` bigint unsigned NOT NULL,
  `login_portal` enum('VISITOR','INTERNAL') COLLATE utf8mb4_unicode_ci NOT NULL,
  `selected_campus_id` bigint unsigned DEFAULT NULL COMMENT 'Auto set to users.primary_campus_id for INTERNAL, NULL for VISITOR',
  `auth_provider_id` bigint unsigned DEFAULT NULL,
  `refresh_token_hash` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Refresh token hash merged into session',
  `refresh_expires_at` datetime DEFAULT NULL,
  `refresh_revoked_at` datetime DEFAULT NULL,
  `ip_address` varchar(45) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `user_agent` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `expires_at` datetime NOT NULL,
  `revoked_at` datetime DEFAULT NULL,
  `revoked_by` bigint unsigned DEFAULT NULL,
  `revoked_reason` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`session_id`),
  UNIQUE KEY `uq_sessions_refresh_hash` (`refresh_token_hash`),
  KEY `idx_sessions_user_active` (`user_id`,`revoked_at`,`expires_at`),
  KEY `idx_sessions_portal_campus` (`login_portal`,`selected_campus_id`),
  KEY `idx_sessions_refresh_active` (`refresh_token_hash`,`refresh_revoked_at`,`refresh_expires_at`),
  KEY `idx_sessions_ip_time` (`ip_address`,`created_at`),
  KEY `idx_sessions_expires_at` (`expires_at`),
  KEY `idx_sessions_revoked_at` (`revoked_at`),
  KEY `fk_sessions_selected_campus` (`selected_campus_id`),
  KEY `fk_sessions_auth_provider` (`auth_provider_id`),
  KEY `fk_sessions_revoked_by` (`revoked_by`),
  CONSTRAINT `fk_sessions_auth_provider` FOREIGN KEY (`auth_provider_id`) REFERENCES `user_auth_providers` (`auth_provider_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_sessions_revoked_by` FOREIGN KEY (`revoked_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_sessions_selected_campus` FOREIGN KEY (`selected_campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_sessions_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=629 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Session + refresh token hash';

-- Dumping data for table pems_db.user_sessions: ~437 rows (approximately)

-- Dumping structure for table pems_db.users
DROP TABLE IF EXISTS `users`;
CREATE TABLE IF NOT EXISTS `users` (
  `user_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `full_name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `email` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `phone` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `nationality` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Quốc tịch của user/visitor',
  `password_hash` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'DEV/local password hash only. Production SSO-only accounts keep this NULL.',
  `role_id` bigint unsigned NOT NULL,
  `sub_role` enum('LEADER','STAFF') COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Only for STAFF/DEPARTMENT',
  `primary_campus_id` bigint unsigned DEFAULT NULL COMMENT 'Campus duy nhất của user nội bộ. VISITOR phải NULL.',
  `department_id` bigint unsigned DEFAULT NULL COMMENT 'STAFF = IC department; DEPARTMENT = GENERAL department',
  `gender` enum('MALE','FEMALE','OTHER') COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'NULL=chưa cung cấp; OTHER=khác Nam/Nữ',
  `avatar_url` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `student_code` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `fe_id` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `status` enum('ACTIVE','INACTIVE','LOCKED','PENDING_EMAIL_CONFIRMATION') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE' COMMENT 'ACTIVE=hoạt động, INACTIVE=tạm ngưng, LOCKED=bị khóa, PENDING_EMAIL_CONFIRMATION=chờ xác nhận email',
  `email_verified_at` datetime DEFAULT NULL COMMENT 'Thời điểm email được xác thực qua SSO lần đầu hoặc xác nhận bởi hệ thống',
  `failed_login_count` int unsigned NOT NULL DEFAULT '0' COMMENT 'Số lần đăng nhập sai local password liên tiếp; reset khi login thành công',
  `locked_until` datetime DEFAULT NULL COMMENT 'Thời điểm hết khóa tạm thời nếu bị lock',
  `created_via` enum('MANUAL_CREATED','VISITOR_FORM','SSO_AUTO_PROVISION') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'MANUAL_CREATED' COMMENT 'MANUAL_CREATED=HO/Staff Leader tạo, VISITOR_FORM=tạo từ form visitor, SSO_AUTO_PROVISION=tạo tự động khi đăng nhập SSO ở cổng Visitor',
  `first_login_at` datetime DEFAULT NULL,
  `last_login_at` datetime DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`user_id`),
  UNIQUE KEY `uq_users_email` (`email`),
  UNIQUE KEY `uq_users_student_code` (`student_code`),
  UNIQUE KEY `uq_users_fe_id` (`fe_id`),
  KEY `idx_users_role_sub_role` (`role_id`,`sub_role`),
  KEY `idx_users_primary_campus` (`primary_campus_id`),
  KEY `idx_users_department` (`department_id`),
  KEY `idx_users_status` (`status`),
  KEY `idx_users_email_status` (`email`,`status`),
  KEY `idx_users_campus_role_status` (`primary_campus_id`,`role_id`,`status`),
  KEY `idx_users_department_status` (`department_id`,`status`),
  KEY `idx_users_created_via` (`created_via`),
  KEY `idx_users_last_login` (`last_login_at`),
  KEY `idx_users_nationality` (`nationality`),
  CONSTRAINT `fk_users_department` FOREIGN KEY (`department_id`) REFERENCES `departments` (`department_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_users_primary_campus` FOREIGN KEY (`primary_campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_users_role` FOREIGN KEY (`role_id`) REFERENCES `roles` (`role_id`) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=252 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Tài khoản chính. Production dùng SSO; LOCAL_PASSWORD chỉ dùng DEV/test.';

-- Dumping data for table pems_db.users: ~115 rows (approximately)
INSERT IGNORE INTO `users` (`user_id`, `full_name`, `email`, `phone`, `nationality`, `password_hash`, `role_id`, `sub_role`, `primary_campus_id`, `department_id`, `gender`, `avatar_url`, `student_code`, `fe_id`, `status`, `email_verified_at`, `failed_login_count`, `locked_until`, `created_via`, `first_login_at`, `last_login_at`, `created_at`, `created_by`, `updated_at`, `updated_by`) VALUES
(1, 'System Administrator', 'admin@fpt.edu.vn', '0901000001', NULL, '$2a$12$cRpFAxEt9VdUg0orDrPRL.oesxu8ID8WSI2YTsNclVZjRtwi57PFi', 1, NULL, 1, NULL, 'MALE', NULL, NULL, 'FE-MANUAL-001', 'ACTIVE', '2026-02-01 08:00:00', 0, NULL, 'MANUAL_CREATED', '2026-02-03 08:15:00', '2026-08-26 11:53:47', '2026-02-01 08:00:00', NULL, '2026-08-26 11:53:46', 1),
(2, 'Head Office Coordinator', 'ho@fpt.edu.vn', '0901000002', NULL, '$2a$12$cRpFAxEt9VdUg0orDrPRL.oesxu8ID8WSI2YTsNclVZjRtwi57PFi', 2, NULL, 1, NULL, 'FEMALE', '/api/files/206/content', NULL, 'FE-MANUAL-002', 'ACTIVE', '2026-02-01 08:05:00', 0, NULL, 'MANUAL_CREATED', '2026-02-03 09:00:00', '2026-08-25 15:31:00', '2026-02-01 08:05:00', 1, '2026-08-25 15:31:00', 2),
(3, 'IC Staff Leader Hà Nội', 'staff.leader.hn@fpt.edu.vn', '0901000003', NULL, '$2a$12$cRpFAxEt9VdUg0orDrPRL.oesxu8ID8WSI2YTsNclVZjRtwi57PFi', 3, 'LEADER', 1, 1, 'MALE', '/api/files/228/content', NULL, 'FE-MANUAL-003', 'ACTIVE', '2026-02-01 08:10:00', 0, NULL, 'MANUAL_CREATED', '2026-02-03 10:00:00', '2026-08-26 16:49:14', '2026-02-01 08:10:00', 2, '2026-08-26 16:49:14', 3),
(4, 'IC Staff Hà Nội', 'staff.hn@fpt.edu.vn', '0901000004', NULL, '$2a$12$cRpFAxEt9VdUg0orDrPRL.oesxu8ID8WSI2YTsNclVZjRtwi57PFi', 3, 'STAFF', 1, 1, 'FEMALE', '/api/files/227/content', NULL, 'FE-MANUAL-004', 'ACTIVE', '2026-02-01 08:15:00', 0, NULL, 'MANUAL_CREATED', '2026-02-03 10:30:00', '2026-08-25 22:14:01', '2026-02-01 08:15:00', 3, '2026-08-25 22:14:01', 4),
(5, 'Department Lead Đào tạo HN', 'dept.leader.hn@fpt.edu.vn', '0901000005', NULL, '$2a$12$cRpFAxEt9VdUg0orDrPRL.oesxu8ID8WSI2YTsNclVZjRtwi57PFi', 4, 'LEADER', 1, 2, 'MALE', NULL, NULL, 'FE-MANUAL-005', 'ACTIVE', '2026-02-01 08:20:00', 0, NULL, 'MANUAL_CREATED', '2026-02-03 11:00:00', '2026-08-26 12:45:27', '2026-02-01 08:20:00', 2, '2026-08-26 12:45:27', 6),
(6, 'Department Staff Đào tạo HN', 'dept.hn@fpt.edu.vn', '0901000006', NULL, '$2a$12$cRpFAxEt9VdUg0orDrPRL.oesxu8ID8WSI2YTsNclVZjRtwi57PFi', 4, 'STAFF', 1, 2, 'FEMALE', NULL, NULL, 'FE-MANUAL-006', 'ACTIVE', '2026-02-01 08:25:00', 0, NULL, 'MANUAL_CREATED', '2026-02-03 11:20:00', '2026-08-26 14:44:06', '2026-02-01 08:25:00', 5, '2026-08-26 14:44:05', 6),
(7, 'Student Buddy Hà Nội', 'student@fpt.edu.vn', '0901000007', NULL, '$2a$12$cRpFAxEt9VdUg0orDrPRL.oesxu8ID8WSI2YTsNclVZjRtwi57PFi', 5, NULL, 1, NULL, 'OTHER', NULL, 'SE190001', 'FE-MANUAL-007', 'ACTIVE', '2026-02-01 08:30:00', 0, NULL, 'MANUAL_CREATED', '2026-02-05 08:30:00', '2026-08-27 04:11:37', '2026-02-01 08:30:00', 3, '2026-08-27 04:11:37', 3),
(8, 'Visitor Example', 'visitor@example.com', '+821012340001', 'Hàn Quốc', '$2a$12$cRpFAxEt9VdUg0orDrPRL.oesxu8ID8WSI2YTsNclVZjRtwi57PFi', 6, NULL, NULL, NULL, 'MALE', NULL, NULL, NULL, 'ACTIVE', '2026-02-01 08:35:00', 0, NULL, 'VISITOR_FORM', '2026-02-05 09:00:00', '2026-08-26 17:27:08', '2026-02-01 08:35:00', NULL, '2026-08-26 17:27:08', NULL),
(9, 'IC Staff Leader TP.HCM', 'staff.leader.hcm@fpt.edu.vn', '0901000009', NULL, '$2a$12$cRpFAxEt9VdUg0orDrPRL.oesxu8ID8WSI2YTsNclVZjRtwi57PFi', 3, 'LEADER', 2, 6, 'FEMALE', NULL, NULL, 'FE-MANUAL-009', 'ACTIVE', '2026-02-01 08:40:00', 0, NULL, 'MANUAL_CREATED', '2026-02-05 09:45:00', '2026-08-20 21:53:52', '2026-02-01 08:40:00', 2, '2026-08-20 21:53:51', 2),
(11, 'IC Staff Leader Đà Nẵng', 'staff.leader.dn@fpt.edu.vn', '0901000011', NULL, '$2a$12$cRpFAxEt9VdUg0orDrPRL.oesxu8ID8WSI2YTsNclVZjRtwi57PFi', 3, 'LEADER', 3, 10, 'MALE', NULL, NULL, 'FE-MANUAL-011', 'ACTIVE', '2026-02-01 08:50:00', 0, NULL, 'MANUAL_CREATED', '2026-02-05 10:30:00', '2026-06-22 09:45:00', '2026-02-01 08:50:00', 2, '2026-06-22 09:45:00', 2),
(13, 'IC Staff Leader Cần Thơ', 'staff.leader.ct@fpt.edu.vn', '0901000013', NULL, '$2a$12$cRpFAxEt9VdUg0orDrPRL.oesxu8ID8WSI2YTsNclVZjRtwi57PFi', 3, 'LEADER', 4, 13, 'FEMALE', NULL, NULL, 'FE-MANUAL-013', 'ACTIVE', '2026-02-01 09:00:00', 0, NULL, 'MANUAL_CREATED', '2026-02-05 11:20:00', '2026-06-22 10:30:00', '2026-02-01 09:00:00', 2, '2026-06-22 10:30:00', 2),
(15, 'IC Staff Leader Quy Nhơn', 'staff.leader.qn@fpt.edu.vn', '0901000015', NULL, '$2a$12$cRpFAxEt9VdUg0orDrPRL.oesxu8ID8WSI2YTsNclVZjRtwi57PFi', 3, 'LEADER', 5, 16, 'MALE', NULL, NULL, 'FE-MANUAL-015', 'ACTIVE', '2026-02-01 09:10:00', 0, NULL, 'MANUAL_CREATED', '2026-02-05 12:00:00', '2026-06-22 11:00:00', '2026-02-01 09:10:00', 2, '2026-06-22 11:00:00', 2);

-- Dumping structure for table pems_db.visit_agendas
DROP TABLE IF EXISTS `visit_agendas`;
CREATE TABLE IF NOT EXISTS `visit_agendas` (
  `agenda_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `visit_instance_id` bigint unsigned NOT NULL,
  `sequence_order` int unsigned NOT NULL,
  `title` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` text COLLATE utf8mb4_unicode_ci,
  `start_time` datetime NOT NULL,
  `end_time` datetime DEFAULT NULL,
  `location` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `responsible_user_id` bigint unsigned DEFAULT NULL,
  `responsible_name` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Tên người phụ trách (nhập tay tự do, không ràng buộc user thật)',
  `source_template_item_id` bigint unsigned DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`agenda_id`),
  UNIQUE KEY `uq_visit_agendas_order` (`visit_instance_id`,`sequence_order`),
  KEY `idx_visit_agendas_time` (`visit_instance_id`,`start_time`),
  KEY `idx_visit_agendas_responsible` (`responsible_user_id`,`start_time`),
  KEY `idx_visit_agendas_source_template_item` (`source_template_item_id`),
  KEY `fk_visit_agendas_created_by` (`created_by`),
  KEY `fk_visit_agendas_updated_by` (`updated_by`),
  CONSTRAINT `fk_visit_agendas_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_agendas_instance` FOREIGN KEY (`visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_instance_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_agendas_responsible_user` FOREIGN KEY (`responsible_user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_agendas_source_template_item` FOREIGN KEY (`source_template_item_id`) REFERENCES `agenda_template_items` (`agenda_template_item_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_agendas_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `visit_agendas_chk_1` CHECK (((`end_time` is null) or (`end_time` > `start_time`)))
) ENGINE=InnoDB AUTO_INCREMENT=45123 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Concrete visit agenda rows per campus instance. start_time/end_time are absolute DATETIME computed from visit_request_campuses.planned_start_at + template offsets.';

-- Dumping data for table pems_db.visit_agendas: ~123 rows (approximately)

-- Dumping structure for table pems_db.visit_expense_items
DROP TABLE IF EXISTS `visit_expense_items`;
CREATE TABLE IF NOT EXISTS `visit_expense_items` (
  `expense_item_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `expense_report_id` bigint unsigned NOT NULL,
  `item_origin` enum('REQUEST_ITEM','MANUAL','ADDITIONAL','DAMAGE_LOSS','OTHER') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'MANUAL',
  `item_name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` text COLLATE utf8mb4_unicode_ci,
  `quantity` decimal(12,2) NOT NULL DEFAULT '1.00',
  `unit_name` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `unit_price` decimal(18,2) NOT NULL DEFAULT '0.00',
  `total_amount` decimal(18,2) GENERATED ALWAYS AS (round((`quantity` * `unit_price`),2)) STORED,
  `item_note` text COLLATE utf8mb4_unicode_ci,
  `display_order` int unsigned NOT NULL DEFAULT '0',
  `row_version` int unsigned NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`expense_item_id`),
  KEY `idx_expense_items_report_order` (`expense_report_id`,`display_order`,`expense_item_id`),
  KEY `idx_expense_items_origin` (`item_origin`),
  KEY `fk_expense_items_created_by` (`created_by`),
  KEY `fk_expense_items_updated_by` (`updated_by`),
  CONSTRAINT `fk_expense_items_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_expense_items_report` FOREIGN KEY (`expense_report_id`) REFERENCES `visit_expense_reports` (`expense_report_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_expense_items_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `ck_expense_item_name` CHECK ((char_length(trim(`item_name`)) > 0)),
  CONSTRAINT `ck_expense_item_quantity` CHECK ((`quantity` > 0)),
  CONSTRAINT `ck_expense_item_unit_price` CHECK ((`unit_price` >= 0))
) ENGINE=InnoDB AUTO_INCREMENT=20 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Chi tiết các khoản chi; thành tiền được tính tự động bằng số lượng nhân đơn giá.';

-- Dumping data for table pems_db.visit_expense_items: ~19 rows (approximately)

-- Dumping structure for table pems_db.visit_expense_report_events
DROP TABLE IF EXISTS `visit_expense_report_events`;
CREATE TABLE IF NOT EXISTS `visit_expense_report_events` (
  `expense_event_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `expense_report_id` bigint unsigned NOT NULL,
  `event_type` enum('CREATED','SAVED','UPDATED','FINALIZED','REOPENED','CANCELLED','EXPORTED') COLLATE utf8mb4_unicode_ci NOT NULL,
  `event_note` text COLLATE utf8mb4_unicode_ci,
  `snapshot_json` json DEFAULT NULL,
  `performed_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `performed_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`expense_event_id`),
  KEY `idx_expense_events_report_time` (`expense_report_id`,`performed_at`),
  KEY `idx_expense_events_actor_time` (`performed_by`,`performed_at`),
  CONSTRAINT `fk_expense_events_actor` FOREIGN KEY (`performed_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_expense_events_report` FOREIGN KEY (`expense_report_id`) REFERENCES `visit_expense_reports` (`expense_report_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=38 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Lịch sử lưu/chốt/mở lại/xuất thống kê chi phí.';

-- Dumping data for table pems_db.visit_expense_report_events: ~37 rows (approximately)

-- Dumping structure for table pems_db.visit_expense_reports
DROP TABLE IF EXISTS `visit_expense_reports`;
CREATE TABLE IF NOT EXISTS `visit_expense_reports` (
  `expense_report_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `visit_instance_id` bigint unsigned NOT NULL,
  `report_scope` enum('GENERAL','LOGISTICS') COLLATE utf8mb4_unicode_ci NOT NULL,
  `logistics_item_id` bigint unsigned DEFAULT NULL,
  `department_id` bigint unsigned DEFAULT NULL,
  `status` enum('DRAFT','SAVED','FINALIZED','CANCELLED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'DRAFT',
  `report_note` text COLLATE utf8mb4_unicode_ci,
  `no_expense` tinyint(1) NOT NULL DEFAULT '0' COMMENT '1 = phòng ban/host xác nhận không phát sinh chi phí cho báo cáo này',
  `currency_code` char(3) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'VND',
  `saved_at` datetime DEFAULT NULL,
  `saved_by` bigint unsigned DEFAULT NULL,
  `finalized_at` datetime DEFAULT NULL,
  `finalized_by` bigint unsigned DEFAULT NULL,
  `cancelled_at` datetime DEFAULT NULL,
  `cancelled_by` bigint unsigned DEFAULT NULL,
  `cancellation_reason` text COLLATE utf8mb4_unicode_ci,
  `row_version` int unsigned NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  `general_instance_guard` bigint unsigned DEFAULT NULL COMMENT 'Trigger-maintained uniqueness guard: visit_instance_id for GENERAL, otherwise NULL',
  `logistics_item_guard` bigint unsigned DEFAULT NULL COMMENT 'Trigger-maintained uniqueness guard: logistics_item_id for LOGISTICS, otherwise NULL',
  PRIMARY KEY (`expense_report_id`),
  UNIQUE KEY `uq_expense_general_instance` (`general_instance_guard`),
  UNIQUE KEY `uq_expense_logistics_item` (`logistics_item_guard`),
  KEY `idx_expense_reports_instance_scope_status` (`visit_instance_id`,`report_scope`,`status`),
  KEY `idx_expense_reports_department_status` (`department_id`,`status`),
  KEY `idx_expense_reports_logistics_item` (`logistics_item_id`),
  KEY `idx_expense_reports_saved_time` (`saved_at`),
  KEY `fk_expense_reports_saved_by` (`saved_by`),
  KEY `fk_expense_reports_finalized_by` (`finalized_by`),
  KEY `fk_expense_reports_cancelled_by` (`cancelled_by`),
  KEY `fk_expense_reports_created_by` (`created_by`),
  KEY `fk_expense_reports_updated_by` (`updated_by`),
  CONSTRAINT `fk_expense_reports_cancelled_by` FOREIGN KEY (`cancelled_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_expense_reports_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_expense_reports_department` FOREIGN KEY (`department_id`) REFERENCES `departments` (`department_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_expense_reports_finalized_by` FOREIGN KEY (`finalized_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_expense_reports_instance` FOREIGN KEY (`visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_instance_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_expense_reports_logistics` FOREIGN KEY (`logistics_item_id`) REFERENCES `visit_logistics_items` (`logistics_item_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_expense_reports_saved_by` FOREIGN KEY (`saved_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_expense_reports_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `ck_expense_report_currency` CHECK ((`currency_code` = _utf8mb4'VND'))
) ENGINE=InnoDB AUTO_INCREMENT=23 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Bảng đầu chi phí theo campus instance; GENERAL do Host nhập, LOGISTICS do Department nhập theo đơn hậu cần.';

-- Dumping data for table pems_db.visit_expense_reports: ~22 rows (approximately)

-- Dumping structure for table pems_db.visit_guest_members
DROP TABLE IF EXISTS `visit_guest_members`;
CREATE TABLE IF NOT EXISTS `visit_guest_members` (
  `guest_member_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `visit_request_id` bigint unsigned NOT NULL,
  `member_type` enum('GUEST','EXTERNAL_SUPPORT') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'GUEST',
  `full_name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `organization` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL,
  `organization_partner_id` bigint unsigned DEFAULT NULL COMMENT 'Thành viên này đã được người đăng ký chọn từ hồ sơ đối tác nào (partners). NULL = tổ chức gõ tay hoặc chưa xác định — matcher có thể gợi ý sau. Cột organization ở trên vẫn là snapshot hiển thị và KHÔNG chạy theo đối tác đổi tên.',
  `job_title` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `nationality` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL,
  `display_order` int unsigned NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`guest_member_id`),
  UNIQUE KEY `uq_vgm_request_member` (`visit_request_id`,`guest_member_id`),
  KEY `idx_guest_members_request` (`visit_request_id`),
  KEY `idx_guest_members_type_order` (`visit_request_id`,`member_type`,`display_order`),
  KEY `idx_vgm_organization_partner` (`organization_partner_id`),
  CONSTRAINT `fk_guest_members_request` FOREIGN KEY (`visit_request_id`) REFERENCES `visit_requests` (`visit_request_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_vgm_organization_partner` FOREIGN KEY (`organization_partner_id`) REFERENCES `partners` (`partner_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `visit_guest_members_chk_1` CHECK ((trim(`full_name`) <> _utf8mb4'')),
  CONSTRAINT `visit_guest_members_chk_2` CHECK ((trim(`organization`) <> _utf8mb4'')),
  CONSTRAINT `visit_guest_members_chk_3` CHECK ((trim(`job_title`) <> _utf8mb4'')),
  CONSTRAINT `visit_guest_members_chk_4` CHECK ((trim(`nationality`) <> _utf8mb4''))
) ENGINE=InnoDB AUTO_INCREMENT=99204 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Danh sách từng người trong đoàn khách. Không lưu consent hình ảnh vì form đã bỏ phần xác nhận sử dụng hình ảnh/thông tin.';

-- Dumping data for table pems_db.visit_guest_members: ~314 rows (approximately)

-- Dumping structure for table pems_db.visit_guest_partner_links
DROP TABLE IF EXISTS `visit_guest_partner_links`;
CREATE TABLE IF NOT EXISTS `visit_guest_partner_links` (
  `link_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `visit_request_id` bigint unsigned NOT NULL,
  `visit_instance_id` bigint unsigned DEFAULT NULL,
  `guest_member_id` bigint unsigned DEFAULT NULL,
  `minute_participant_id` bigint unsigned DEFAULT NULL,
  `partner_id` bigint unsigned NOT NULL,
  `partner_contact_id` bigint unsigned DEFAULT NULL,
  `match_source` enum('AUTO_NAME','AUTO_EMAIL_DOMAIN','MANUAL','CREATED_FROM_GUEST','BUSINESS_CARD_OCR','REGISTRATION_SELECTED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'MANUAL' COMMENT 'Nguồn của quan hệ. REGISTRATION_SELECTED = người đăng ký tự chọn hồ sơ đối tác ngay trên form (danh tính ổn định, không phải máy suy). AUTO_NAME/AUTO_EMAIL_DOMAIN = hệ thống suy ra. MANUAL = người dùng bấm liên kết ở màn biên bản.',
  `match_status` enum('SUGGESTED','CONFIRMED','REJECTED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'CONFIRMED',
  `confidence_score` decimal(5,2) DEFAULT NULL,
  `note` text COLLATE utf8mb4_unicode_ci,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`link_id`),
  UNIQUE KEY `uq_vgpl_guest_member` (`guest_member_id`),
  KEY `idx_vgpl_visit_request` (`visit_request_id`),
  KEY `idx_vgpl_visit_instance` (`visit_instance_id`),
  KEY `idx_vgpl_guest_member` (`guest_member_id`),
  KEY `idx_vgpl_minute_participant` (`minute_participant_id`),
  KEY `idx_vgpl_partner` (`partner_id`),
  KEY `idx_vgpl_contact` (`partner_contact_id`),
  KEY `idx_vgpl_status` (`match_status`),
  KEY `idx_vgpl_source` (`match_source`),
  KEY `fk_vgpl_created_by` (`created_by`),
  KEY `fk_vgpl_updated_by` (`updated_by`),
  CONSTRAINT `fk_vgpl_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_vgpl_guest_member` FOREIGN KEY (`guest_member_id`) REFERENCES `visit_guest_members` (`guest_member_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_vgpl_minute_participant` FOREIGN KEY (`minute_participant_id`) REFERENCES `minute_participants` (`minute_participant_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_vgpl_partner` FOREIGN KEY (`partner_id`) REFERENCES `partners` (`partner_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_vgpl_partner_contact` FOREIGN KEY (`partner_contact_id`) REFERENCES `partner_contacts` (`contact_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_vgpl_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_vgpl_visit_instance` FOREIGN KEY (`visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_instance_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_vgpl_visit_request` FOREIGN KEY (`visit_request_id`) REFERENCES `visit_requests` (`visit_request_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=50 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Links visit guests/minute participants to partner profiles for partner labels and partner history.';

-- Dumping data for table pems_db.visit_guest_partner_links: ~49 rows (approximately)

-- Dumping structure for table pems_db.visit_instance_amendment_changes
DROP TABLE IF EXISTS `visit_instance_amendment_changes`;
CREATE TABLE IF NOT EXISTS `visit_instance_amendment_changes` (
  `amendment_change_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `amendment_id` bigint unsigned NOT NULL,
  `field_path` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `change_class` varchar(40) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'SAFE | APPROVAL_SENSITIVE | STRUCTURAL | PRIVACY_URGENT',
  `old_value_json` json DEFAULT NULL,
  `new_value_json` json DEFAULT NULL,
  `is_sensitive` tinyint(1) NOT NULL DEFAULT '0',
  `display_order` int unsigned NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`amendment_change_id`),
  KEY `idx_amendment_change_amendment` (`amendment_id`,`display_order`),
  CONSTRAINT `fk_amendment_change_amendment` FOREIGN KEY (`amendment_id`) REFERENCES `visit_instance_amendments` (`amendment_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=11 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Field-level proposal của một amendment (immutable sau PENDING_APPROVAL).';

-- Dumping data for table pems_db.visit_instance_amendment_changes: ~10 rows (approximately)

-- Dumping structure for table pems_db.visit_instance_amendments
DROP TABLE IF EXISTS `visit_instance_amendments`;
CREATE TABLE IF NOT EXISTS `visit_instance_amendments` (
  `amendment_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `visit_request_id` bigint unsigned NOT NULL,
  `visit_instance_id` bigint unsigned NOT NULL,
  `amendment_no` int unsigned NOT NULL,
  `status` enum('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED','WITHDRAWN','EXPIRED','CANCELLED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'DRAFT',
  `base_form_revision` int unsigned NOT NULL,
  `base_approval_revision` int unsigned NOT NULL,
  `requested_by` bigint unsigned NOT NULL,
  `requested_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `reason` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `decided_by` bigint unsigned DEFAULT NULL,
  `decided_at` datetime DEFAULT NULL,
  `decision_note` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `expires_at` datetime DEFAULT NULL,
  `withdrawn_at` datetime DEFAULT NULL,
  `expected_instance_row_version` int unsigned NOT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `amendment_pending_guard` bigint unsigned GENERATED ALWAYS AS ((case when (`status` = _utf8mb4'PENDING_APPROVAL') then `visit_instance_id` else NULL end)) VIRTUAL,
  PRIMARY KEY (`amendment_id`),
  UNIQUE KEY `uq_amendment_instance_no` (`visit_instance_id`,`amendment_no`),
  UNIQUE KEY `uq_amendment_pending` (`amendment_pending_guard`),
  KEY `idx_amendment_instance_status_time` (`visit_instance_id`,`status`,`requested_at`),
  KEY `idx_amendment_request` (`visit_request_id`,`status`),
  KEY `fk_amendment_instance` (`visit_request_id`,`visit_instance_id`),
  KEY `fk_amendment_requested_by` (`requested_by`),
  KEY `fk_amendment_decided_by` (`decided_by`),
  CONSTRAINT `fk_amendment_decided_by` FOREIGN KEY (`decided_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_amendment_instance` FOREIGN KEY (`visit_request_id`, `visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_request_id`, `visit_instance_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_amendment_requested_by` FOREIGN KEY (`requested_by`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Đề xuất thay đổi approval-sensitive cho campus instance đã duyệt. Chỉ một PENDING_APPROVAL/instance.';

-- Dumping data for table pems_db.visit_instance_amendments: ~6 rows (approximately)

-- Dumping structure for table pems_db.visit_instance_form_details
DROP TABLE IF EXISTS `visit_instance_form_details`;
CREATE TABLE IF NOT EXISTS `visit_instance_form_details` (
  `visit_instance_id` bigint unsigned NOT NULL,
  `delegation_name` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Tên đoàn hiển thị tại campus này',
  `visit_type` enum('CAMPUS_TOUR','MEETING','WORKSHOP','SIGNING_CEREMONY','EXCHANGE','OTHER') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'CAMPUS_TOUR',
  `visit_type_other` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `purpose` text COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Mục đích tại campus này',
  `working_content` text COLLATE utf8mb4_unicode_ci COMMENT 'Nội dung làm việc tại campus này',
  `operational_contact_full_name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Tên đầu mối làm việc tại cơ sở (snapshot). Tên trùng KHÔNG phải bằng chứng cùng người.',
  `operational_contact_organization` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Optional; blank normalized to NULL by the create/edit service (ck rejects empty string)',
  `operational_contact_job_title` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'BẮT BUỘC. Chức vụ đầu mối (snapshot). Là thứ cho campus biết người bên kia đầu dây tự quyết được lịch hay phải đi hỏi; màn chi tiết luôn hiển thị đủ Họ tên/Đơn vị/Chức vụ/SĐT/Email.',
  `operational_contact_phone` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'SĐT đầu mối (snapshot, optional — đầu mối chỉ có email vẫn dùng được). SĐT trùng KHÔNG phải bằng chứng cùng người; blank normalize thành NULL.',
  `operational_contact_email` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'BẮT BUỘC. Normalize tại application boundary; là email duy nhất dùng để bind lời mời xác nhận đầu mối cho campus này. Danh tính runtime đọc từ visit_request_campuses.operational_contact_user_id, KHÔNG từ email này.',
  `operational_contact_guest_member_id` bigint unsigned DEFAULT NULL COMMENT 'Đầu mối của cơ sở này LÀ thành viên nào trong đoàn (visit_guest_members). NULL = chưa nối hoặc đầu mối không đi cùng đoàn — biên bản khi đó lấy từ snapshot. Snapshot operational_contact_* vẫn giữ nguyên để phục vụ kiểm chứng.',
  `working_language` enum('VI','EN') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'EN',
  `transportation_note` text COLLATE utf8mb4_unicode_ci,
  `media_consent_status` enum('AGREED','DECLINED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'DECLINED',
  `notes` text COLLATE utf8mb4_unicode_ci COMMENT 'Ghi chú bổ sung chung của khách gửi FPTU tại campus này',
  `form_revision` int unsigned NOT NULL DEFAULT '1',
  `approval_revision` int unsigned NOT NULL DEFAULT '1',
  `row_version` int unsigned NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`visit_instance_id`),
  KEY `idx_vifd_visit_type` (`visit_type`),
  KEY `idx_vifd_language` (`working_language`),
  KEY `idx_vifd_media_consent` (`media_consent_status`),
  KEY `idx_vifd_op_contact_email` (`operational_contact_email`),
  KEY `idx_vifd_op_contact_member` (`operational_contact_guest_member_id`),
  FULLTEXT KEY `ft_vifd_search` (`delegation_name`,`purpose`,`working_content`,`operational_contact_full_name`,`operational_contact_organization`,`operational_contact_email`),
  CONSTRAINT `fk_vifd_instance` FOREIGN KEY (`visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_instance_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_vifd_op_contact_member` FOREIGN KEY (`operational_contact_guest_member_id`) REFERENCES `visit_guest_members` (`guest_member_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `ck_vifd_delegation_name` CHECK ((trim(`delegation_name`) <> _utf8mb4'')),
  CONSTRAINT `ck_vifd_op_contact_email` CHECK ((trim(`operational_contact_email`) <> _utf8mb4'')),
  CONSTRAINT `ck_vifd_op_contact_job_title` CHECK ((trim(`operational_contact_job_title`) <> _utf8mb4'')),
  CONSTRAINT `ck_vifd_op_contact_name` CHECK ((trim(`operational_contact_full_name`) <> _utf8mb4'')),
  CONSTRAINT `ck_vifd_op_contact_org` CHECK (((`operational_contact_organization` is null) or (trim(`operational_contact_organization`) <> _utf8mb4''))),
  CONSTRAINT `ck_vifd_op_contact_phone` CHECK (((`operational_contact_phone` is null) or (trim(`operational_contact_phone`) <> _utf8mb4''))),
  CONSTRAINT `ck_vifd_purpose` CHECK ((trim(`purpose`) <> _utf8mb4'')),
  CONSTRAINT `ck_vifd_visit_type_other` CHECK (((`visit_type` <> _utf8mb4'OTHER') or ((`visit_type_other` is not null) and (trim(`visit_type_other`) <> _utf8mb4''))))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Snapshot form đầy đủ, độc lập cho từng campus instance (v2). Mỗi row là bản hoàn chỉnh; không có cờ same_as_other_campus.';

-- Dumping data for table pems_db.visit_instance_form_details: ~127 rows (approximately)

-- Dumping structure for table pems_db.visit_instance_form_revision_history
DROP TABLE IF EXISTS `visit_instance_form_revision_history`;
CREATE TABLE IF NOT EXISTS `visit_instance_form_revision_history` (
  `revision_history_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `visit_request_id` bigint unsigned NOT NULL,
  `visit_instance_id` bigint unsigned NOT NULL,
  `form_revision` int unsigned NOT NULL,
  `approval_revision` int unsigned NOT NULL,
  `source_type` enum('CREATE','SAFE_EDIT','PENDING_EDIT','AMENDMENT_APPLIED','MIGRATION','RESUBMIT') COLLATE utf8mb4_unicode_ci NOT NULL,
  `source_id` bigint unsigned DEFAULT NULL,
  `snapshot_json` json NOT NULL,
  `applied_by` bigint unsigned DEFAULT NULL,
  `applied_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `reason` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`revision_history_id`),
  UNIQUE KEY `uq_vifrh_instance_form_revision` (`visit_instance_id`,`form_revision`),
  KEY `idx_vifrh_request_time` (`visit_request_id`,`applied_at`),
  KEY `idx_vifrh_source` (`source_type`,`source_id`),
  KEY `fk_vifrh_instance` (`visit_request_id`,`visit_instance_id`),
  KEY `fk_vifrh_applied_by` (`applied_by`),
  CONSTRAINT `fk_vifrh_applied_by` FOREIGN KEY (`applied_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_vifrh_instance` FOREIGN KEY (`visit_request_id`, `visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_request_id`, `visit_instance_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=46072 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Lịch sử revision per-instance; mỗi form_revision là snapshot immutable.';

-- Dumping data for table pems_db.visit_instance_form_revision_history: ~115 rows (approximately)

-- Dumping structure for table pems_db.visit_instance_guest_members
DROP TABLE IF EXISTS `visit_instance_guest_members`;
CREATE TABLE IF NOT EXISTS `visit_instance_guest_members` (
  `visit_request_id` bigint unsigned NOT NULL,
  `visit_instance_id` bigint unsigned NOT NULL,
  `guest_member_id` bigint unsigned NOT NULL,
  `display_order` int unsigned NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`visit_instance_id`,`guest_member_id`),
  KEY `idx_vigm_request` (`visit_request_id`),
  KEY `idx_vigm_member` (`guest_member_id`),
  KEY `idx_vigm_instance_order` (`visit_instance_id`,`display_order`),
  KEY `fk_vigm_instance` (`visit_request_id`,`visit_instance_id`),
  KEY `fk_vigm_member` (`visit_request_id`,`guest_member_id`),
  CONSTRAINT `fk_vigm_instance` FOREIGN KEY (`visit_request_id`, `visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_request_id`, `visit_instance_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_vigm_member` FOREIGN KEY (`visit_request_id`, `guest_member_id`) REFERENCES `visit_guest_members` (`visit_request_id`, `guest_member_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Bảng nối khách/đội hỗ trợ theo campus instance. Composite FK chống cross-request link; copy-on-write khi campus dùng chung member cũ được sửa.';

-- Dumping data for table pems_db.visit_instance_guest_members: ~377 rows (approximately)

-- Dumping structure for table pems_db.visit_instance_reminder_settings
DROP TABLE IF EXISTS `visit_instance_reminder_settings`;
CREATE TABLE IF NOT EXISTS `visit_instance_reminder_settings` (
  `reminder_setting_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `visit_instance_id` bigint unsigned NOT NULL,
  `channel` enum('IN_APP','EMAIL') COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'IN_APP=thông báo trong hệ thống, EMAIL=gửi email',
  `target_group` enum('HOST','PARTICIPANTS','HOST_AND_PARTICIPANTS') COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Nhóm người nhận cảnh báo',
  `offset_minutes` int unsigned NOT NULL COMMENT 'Số phút nhắc trước planned_start_at (Nhắc trước bao lâu)',
  `scheduled_at` datetime NOT NULL COMMENT 'Thời điểm hệ thống sẽ gửi nhắc',
  `status` enum('PENDING','SENT','CANCELLED','FAILED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING' COMMENT 'PENDING=chờ gửi, SENT=đã gửi, CANCELLED=đã hủy, FAILED=gửi lỗi',
  `last_dispatched_at` datetime DEFAULT NULL,
  `error_message` text COLLATE utf8mb4_unicode_ci,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`reminder_setting_id`),
  UNIQUE KEY `uq_visit_reminder_channel_target` (`visit_instance_id`,`channel`,`target_group`),
  KEY `idx_visit_reminder_schedule` (`status`,`scheduled_at`),
  KEY `idx_visit_reminder_instance` (`visit_instance_id`),
  KEY `idx_visit_reminder_created_by` (`created_by`),
  KEY `idx_visit_reminder_updated_by` (`updated_by`),
  CONSTRAINT `fk_visit_reminder_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_reminder_instance` FOREIGN KEY (`visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_instance_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_reminder_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=12 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Cấu hình lịch gửi cảnh báo/email cho Host và người tham gia theo campus visit instance.';

-- Dumping data for table pems_db.visit_instance_reminder_settings: ~11 rows (approximately)

-- Dumping structure for table pems_db.visit_logistics_assignment_attempts
DROP TABLE IF EXISTS `visit_logistics_assignment_attempts`;
CREATE TABLE IF NOT EXISTS `visit_logistics_assignment_attempts` (
  `assignment_attempt_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `logistics_item_id` bigint unsigned NOT NULL,
  `assignee_user_id` bigint unsigned NOT NULL COMMENT 'Nhân sự phòng ban được Department Leader phân công trong lần giao này',
  `assigned_by` bigint unsigned NOT NULL COMMENT 'Department Leader hoặc người có quyền đã phân công',
  `assigned_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT 'Thời điểm phân công',
  `status` enum('PENDING','ACCEPTED','DECLINED','CANCELLED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING' COMMENT 'Trạng thái phản hồi của riêng lần phân công này',
  `responded_at` datetime DEFAULT NULL COMMENT 'Thời điểm nhân sự phản hồi ACCEPTED/DECLINED',
  `response_note` text COLLATE utf8mb4_unicode_ci COMMENT 'Lý do từ chối hoặc ghi chú phản hồi; bắt buộc khi status=DECLINED',
  `response_source` enum('PORTAL','EMAIL_TOKEN','SYSTEM') COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Nguồn phản hồi: trong portal, nút email token, hoặc hệ thống',
  `cancelled_by` bigint unsigned DEFAULT NULL COMMENT 'Người hủy lần phân công khi cần, không dùng cho transfer nhiệm vụ đã nhận',
  `cancelled_at` datetime DEFAULT NULL COMMENT 'Thời điểm hủy lần phân công',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`assignment_attempt_id`),
  KEY `idx_vla_item_status` (`logistics_item_id`,`status`),
  KEY `idx_vla_assignee_status` (`assignee_user_id`,`status`),
  KEY `idx_vla_assigned_by_time` (`assigned_by`,`assigned_at`),
  KEY `idx_vla_responded_at` (`responded_at`),
  KEY `idx_vla_cancelled_by` (`cancelled_by`),
  CONSTRAINT `fk_vla_assigned_by` FOREIGN KEY (`assigned_by`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_vla_assignee` FOREIGN KEY (`assignee_user_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_vla_cancelled_by` FOREIGN KEY (`cancelled_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_vla_item` FOREIGN KEY (`logistics_item_id`) REFERENCES `visit_logistics_items` (`logistics_item_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=16 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Lịch sử từng lần phân công logistics item cho nhân sự phòng ban; cho phép phân công lại sau khi lần trước DECLINED.';

-- Dumping data for table pems_db.visit_logistics_assignment_attempts: ~15 rows (approximately)

-- Dumping structure for table pems_db.visit_logistics_item_handovers
DROP TABLE IF EXISTS `visit_logistics_item_handovers`;
CREATE TABLE IF NOT EXISTS `visit_logistics_item_handovers` (
  `handover_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `logistics_item_id` bigint unsigned NOT NULL,
  `handover_type` enum('BORROW','RETURN') COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'BORROW=lúc giao/mượn đồ, RETURN=lúc trả/nhận lại đồ',
  `borrower_signed_by` bigint unsigned DEFAULT NULL COMMENT 'Bên mượn ký: BORROW=ký nhận, RETURN=ký trả',
  `borrower_signed_at` datetime DEFAULT NULL,
  `provider_signed_by` bigint unsigned DEFAULT NULL COMMENT 'Bên cho mượn ký: BORROW=ký bàn giao, RETURN=ký nhận lại',
  `provider_signed_at` datetime DEFAULT NULL,
  `item_condition` enum('GOOD','DAMAGED','MISSING','OTHER') COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `condition_note` text COLLATE utf8mb4_unicode_ci,
  `attachment_file_id` bigint unsigned DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`handover_id`),
  UNIQUE KEY `uq_logistics_handover_type` (`logistics_item_id`,`handover_type`),
  KEY `idx_handover_item_type` (`logistics_item_id`,`handover_type`),
  KEY `idx_handover_borrower_signed` (`borrower_signed_by`,`borrower_signed_at`),
  KEY `idx_handover_provider_signed` (`provider_signed_by`,`provider_signed_at`),
  KEY `idx_handover_attachment_file` (`attachment_file_id`),
  KEY `idx_handover_created_by` (`created_by`),
  CONSTRAINT `fk_lh_attachment_file` FOREIGN KEY (`attachment_file_id`) REFERENCES `files` (`file_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_lh_borrower_signed_by` FOREIGN KEY (`borrower_signed_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_lh_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_lh_item` FOREIGN KEY (`logistics_item_id`) REFERENCES `visit_logistics_items` (`logistics_item_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_lh_provider_signed_by` FOREIGN KEY (`provider_signed_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=49520 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Bảng lưu ký nhận/ký trả đồ mượn cho logistics item';

-- Dumping data for table pems_db.visit_logistics_item_handovers: ~25 rows (approximately)

-- Dumping structure for table pems_db.visit_logistics_items
DROP TABLE IF EXISTS `visit_logistics_items`;
CREATE TABLE IF NOT EXISTS `visit_logistics_items` (
  `logistics_item_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `visit_instance_id` bigint unsigned NOT NULL,
  `item_type` enum('ROOM','TRANSPORT','MEAL','EQUIPMENT','BANNER','LED','OTHER') COLLATE utf8mb4_unicode_ci NOT NULL,
  `title` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `description` text COLLATE utf8mb4_unicode_ci COMMENT 'Nội dung chi tiết công việc gốc',
  `coordination_mode` enum('SYSTEM_REQUEST','OFFLINE_COORDINATED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'SYSTEM_REQUEST' COMMENT 'SYSTEM_REQUEST=gửi yêu cầu xử lý qua hệ thống, OFFLINE_COORDINATED=đã trao đổi/xử lý bên ngoài hệ thống',
  `offline_coordination_note` text COLLATE utf8mb4_unicode_ci COMMENT 'Ghi chú khi yêu cầu đã được trao đổi/xử lý bên ngoài hệ thống',
  `quantity` int unsigned DEFAULT NULL COMMENT 'Số lượng yêu cầu gốc',
  `usage_start_at` datetime DEFAULT NULL COMMENT 'Thời gian bắt đầu sử dụng resource',
  `usage_end_at` datetime DEFAULT NULL COMMENT 'Thời gian kết thúc sử dụng resource',
  `status` enum('REQUESTED','CHANGE_PROPOSED','ASSIGNED','ACCEPTED','IN_PROGRESS','DONE','REJECTED','DECLINED','CANCELLED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'REQUESTED' COMMENT 'REQUESTED=đơn đã gửi tới phòng ban; DECLINED=nhân sự từ chối lần phân công; REJECTED=từ chối toàn bộ đơn yêu cầu',
  `priority` enum('LOW','MEDIUM','HIGH','URGENT') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'MEDIUM',
  `requested_by` bigint unsigned DEFAULT NULL COMMENT 'Người gửi yêu cầu hậu cần/resource',
  `requested_to_department_id` bigint unsigned DEFAULT NULL COMMENT 'Phòng ban được yêu cầu xử lý',
  `requested_at` datetime DEFAULT NULL COMMENT 'Thời điểm gửi yêu cầu',
  `received_by` bigint unsigned DEFAULT NULL COMMENT 'Trưởng phòng/người tiếp nhận yêu cầu',
  `received_at` datetime DEFAULT NULL COMMENT 'Thời điểm tiếp nhận yêu cầu',
  `assigned_to_user_id` bigint unsigned DEFAULT NULL COMMENT 'Nhân viên được giao xử lý chính',
  `assigned_by` bigint unsigned DEFAULT NULL COMMENT 'Người phân công',
  `assigned_at` datetime DEFAULT NULL COMMENT 'Thời điểm phân công',
  `assignee_accepted_at` datetime DEFAULT NULL COMMENT 'Thời điểm nhân viên xác nhận nhận nhiệm vụ',
  `assignee_response_note` text COLLATE utf8mb4_unicode_ci COMMENT 'Ghi chú khi nhân viên nhận/từ chối nếu có',
  `due_at` datetime DEFAULT NULL COMMENT 'Deadline hoàn thành hạng mục',
  `completed_at` datetime DEFAULT NULL COMMENT 'Thời điểm hoàn thành',
  `proposed_by` bigint unsigned DEFAULT NULL COMMENT 'Người gửi đề xuất thay đổi',
  `proposed_at` datetime DEFAULT NULL COMMENT 'Thời điểm gửi đề xuất thay đổi',
  `proposed_quantity` int unsigned DEFAULT NULL COMMENT 'Số lượng được đề xuất thay đổi',
  `proposed_usage_start_at` datetime DEFAULT NULL COMMENT 'Thời gian bắt đầu sử dụng được đề xuất',
  `proposed_usage_end_at` datetime DEFAULT NULL COMMENT 'Thời gian kết thúc sử dụng được đề xuất',
  `proposed_description` text COLLATE utf8mb4_unicode_ci COMMENT 'Nội dung chi tiết công việc được đề xuất thay đổi',
  `proposal_note` text COLLATE utf8mb4_unicode_ci COMMENT 'Lý do/ghi chú đề xuất thay đổi',
  `proposal_responded_by` bigint unsigned DEFAULT NULL COMMENT 'Người xác nhận/từ chối đề xuất',
  `proposal_responded_at` datetime DEFAULT NULL COMMENT 'Thời điểm xác nhận/từ chối đề xuất',
  `proposal_response` enum('ACCEPTED','REJECTED') COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Kết quả phản hồi đề xuất',
  `proposal_response_note` text COLLATE utf8mb4_unicode_ci COMMENT 'Ghi chú phản hồi đề xuất',
  `decision_note` text COLLATE utf8mb4_unicode_ci COMMENT 'Lý do reject/cancel hoặc ghi chú xử lý',
  `row_version` int unsigned NOT NULL DEFAULT '0' COMMENT 'Optimistic concurrency token',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`logistics_item_id`),
  KEY `idx_logistics_instance_status` (`visit_instance_id`,`status`),
  KEY `idx_logistics_item_status` (`item_type`,`status`),
  KEY `idx_logistics_department_status` (`requested_to_department_id`,`status`),
  KEY `idx_logistics_assignee_status` (`assigned_to_user_id`,`status`),
  KEY `idx_logistics_requested_by_time` (`requested_by`,`requested_at`),
  KEY `idx_logistics_received_by_time` (`received_by`,`received_at`),
  KEY `idx_logistics_usage_time` (`usage_start_at`,`usage_end_at`),
  KEY `idx_logistics_due` (`due_at`),
  KEY `idx_logistics_priority_due` (`priority`,`due_at`),
  KEY `idx_logistics_proposed_by_time` (`proposed_by`,`proposed_at`),
  KEY `fk_logistics_assigned_by` (`assigned_by`),
  KEY `fk_logistics_proposal_responded_by` (`proposal_responded_by`),
  CONSTRAINT `fk_logistics_assigned_by` FOREIGN KEY (`assigned_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_logistics_assigned_to` FOREIGN KEY (`assigned_to_user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_logistics_instance` FOREIGN KEY (`visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_instance_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_logistics_proposal_responded_by` FOREIGN KEY (`proposal_responded_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_logistics_proposed_by` FOREIGN KEY (`proposed_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_logistics_received_by` FOREIGN KEY (`received_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_logistics_requested_by` FOREIGN KEY (`requested_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_logistics_requested_to_department` FOREIGN KEY (`requested_to_department_id`) REFERENCES `departments` (`department_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `visit_logistics_items_chk_1` CHECK (((`quantity` is null) or (`quantity` >= 1))),
  CONSTRAINT `visit_logistics_items_chk_2` CHECK (((`usage_end_at` is null) or (`usage_start_at` is null) or (`usage_end_at` > `usage_start_at`))),
  CONSTRAINT `visit_logistics_items_chk_3` CHECK (((`proposed_quantity` is null) or (`proposed_quantity` >= 0))),
  CONSTRAINT `visit_logistics_items_chk_4` CHECK (((`proposed_usage_end_at` is null) or (`proposed_usage_start_at` is null) or (`proposed_usage_end_at` > `proposed_usage_start_at`)))
) ENGINE=InnoDB AUTO_INCREMENT=99026 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Yêu cầu hậu cần/resource cho visit: gửi yêu cầu, đề xuất thay đổi, tiếp nhận, phân công, xác nhận và hoàn thành. Ký mượn/ký trả lưu ở visit_logistics_item_handovers.';

-- Dumping data for table pems_db.visit_logistics_items: ~44 rows (approximately)

-- Dumping structure for table pems_db.visit_participants
DROP TABLE IF EXISTS `visit_participants`;
CREATE TABLE IF NOT EXISTS `visit_participants` (
  `participant_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `visit_instance_id` bigint unsigned NOT NULL,
  `user_id` bigint unsigned NOT NULL,
  `participant_role` enum('IC_HOST','IC_SUPPORT','DEPT_SUPPORT','STUDENT') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'IC_SUPPORT',
  `is_host` tinyint(1) NOT NULL DEFAULT '0',
  `status` enum('INVITED','ACCEPTED','DECLINED','ASSIGNED','REMOVED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'INVITED',
  `invited_by` bigint unsigned DEFAULT NULL,
  `invited_at` datetime DEFAULT NULL,
  `responded_at` datetime DEFAULT NULL,
  `assigned_by` bigint unsigned DEFAULT NULL,
  `assigned_at` datetime DEFAULT NULL,
  `note` text COLLATE utf8mb4_unicode_ci,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`participant_id`),
  UNIQUE KEY `uq_visit_participants_user` (`visit_instance_id`,`user_id`),
  KEY `idx_visit_participants_one_host_lookup` (`visit_instance_id`,`is_host`),
  KEY `idx_visit_participants_user_status` (`user_id`,`status`),
  KEY `idx_visit_participants_instance` (`visit_instance_id`),
  KEY `idx_visit_participants_role_status` (`participant_role`,`status`),
  KEY `fk_visit_participants_invited_by` (`invited_by`),
  KEY `fk_visit_participants_assigned_by` (`assigned_by`),
  CONSTRAINT `fk_visit_participants_assigned_by` FOREIGN KEY (`assigned_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_participants_instance` FOREIGN KEY (`visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_instance_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_participants_invited_by` FOREIGN KEY (`invited_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_participants_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=99552 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Người nội bộ tham gia visit instance. Chỉ gồm IC_HOST, IC_SUPPORT, DEPT_SUPPORT, STUDENT. Host chính lưu bằng is_host.';

-- Dumping data for table pems_db.visit_participants: ~113 rows (approximately)

-- Dumping structure for table pems_db.visit_photo_face_detections
DROP TABLE IF EXISTS `visit_photo_face_detections`;
CREATE TABLE IF NOT EXISTS `visit_photo_face_detections` (
  `face_detection_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `face_scan_id` bigint unsigned NOT NULL,
  `visit_request_id` bigint unsigned NOT NULL,
  `visit_instance_id` bigint unsigned NOT NULL,
  `file_id` bigint unsigned NOT NULL,
  `face_index` int unsigned NOT NULL COMMENT 'One-based stable index inside this scan result',
  `bounding_box_x` decimal(10,8) NOT NULL COMMENT 'Normalized left coordinate from 0 to 1',
  `bounding_box_y` decimal(10,8) NOT NULL COMMENT 'Normalized top coordinate from 0 to 1',
  `bounding_box_width` decimal(10,8) NOT NULL COMMENT 'Normalized width from 0 to 1',
  `bounding_box_height` decimal(10,8) NOT NULL COMMENT 'Normalized height from 0 to 1',
  `detection_confidence` decimal(6,5) NOT NULL COMMENT 'Google Vision detection confidence from 0 to 1',
  `guest_member_id` bigint unsigned DEFAULT NULL COMMENT 'Manual selection; must belong to this exact visit instance',
  `face_tag_id` bigint unsigned DEFAULT NULL COMMENT 'Final canonical photo_face_tags row created after confirmation',
  `review_status` enum('DETECTED','CONFIRMED','IGNORED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'DETECTED',
  `review_note` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `reviewed_at` datetime DEFAULT NULL,
  `reviewed_by` bigint unsigned DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`face_detection_id`),
  UNIQUE KEY `uq_visit_photo_face_detection_index` (`face_scan_id`,`face_index`),
  UNIQUE KEY `uq_visit_photo_face_detection_guest` (`face_scan_id`,`guest_member_id`),
  UNIQUE KEY `uq_visit_photo_face_detection_tag` (`face_tag_id`),
  KEY `idx_visit_photo_face_detection_instance` (`visit_instance_id`,`review_status`),
  KEY `idx_visit_photo_face_detection_guest` (`guest_member_id`),
  KEY `idx_visit_photo_face_detection_status` (`review_status`,`created_at`),
  KEY `idx_visit_photo_face_detection_reviewer` (`reviewed_by`,`reviewed_at`),
  KEY `fk_visit_photo_face_detection_scan_scope` (`face_scan_id`,`visit_request_id`,`visit_instance_id`,`file_id`),
  KEY `fk_visit_photo_face_detection_instance_guest` (`visit_instance_id`,`guest_member_id`),
  CONSTRAINT `fk_visit_photo_face_detection_instance_guest` FOREIGN KEY (`visit_instance_id`, `guest_member_id`) REFERENCES `visit_instance_guest_members` (`visit_instance_id`, `guest_member_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_visit_photo_face_detection_reviewed_by` FOREIGN KEY (`reviewed_by`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_visit_photo_face_detection_scan_scope` FOREIGN KEY (`face_scan_id`, `visit_request_id`, `visit_instance_id`, `file_id`) REFERENCES `visit_photo_face_scans` (`face_scan_id`, `visit_request_id`, `visit_instance_id`, `file_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_photo_face_detection_tag` FOREIGN KEY (`face_tag_id`) REFERENCES `photo_face_tags` (`face_tag_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `ck_visit_photo_face_detection_confidence` CHECK (((`detection_confidence` >= 0) and (`detection_confidence` <= 1))),
  CONSTRAINT `ck_visit_photo_face_detection_height` CHECK (((`bounding_box_height` > 0) and (`bounding_box_height` <= 1) and ((`bounding_box_y` + `bounding_box_height`) <= 1))),
  CONSTRAINT `ck_visit_photo_face_detection_index` CHECK ((`face_index` > 0)),
  CONSTRAINT `ck_visit_photo_face_detection_review_state` CHECK ((((`review_status` = _utf8mb4'DETECTED') and (`face_tag_id` is null) and (`reviewed_by` is null) and (`reviewed_at` is null)) or ((`review_status` = _utf8mb4'CONFIRMED') and (`guest_member_id` is not null) and (`face_tag_id` is not null) and (`reviewed_by` is not null) and (`reviewed_at` is not null)) or ((`review_status` = _utf8mb4'IGNORED') and (`guest_member_id` is null) and (`face_tag_id` is null) and (`reviewed_by` is not null) and (`reviewed_at` is not null)))),
  CONSTRAINT `ck_visit_photo_face_detection_width` CHECK (((`bounding_box_width` > 0) and (`bounding_box_width` <= 1) and ((`bounding_box_x` + `bounding_box_width`) <= 1))),
  CONSTRAINT `ck_visit_photo_face_detection_x` CHECK (((`bounding_box_x` >= 0) and (`bounding_box_x` <= 1))),
  CONSTRAINT `ck_visit_photo_face_detection_y` CHECK (((`bounding_box_y` >= 0) and (`bounding_box_y` <= 1)))
) ENGINE=InnoDB AUTO_INCREMENT=87 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Individual Google Vision face boxes awaiting manual assignment to a guest in the exact campus visit instance. Confirmed rows link to existing photo_face_tags.';

-- Dumping data for table pems_db.visit_photo_face_detections: ~86 rows (approximately)

-- Dumping structure for table pems_db.visit_photo_face_scans
DROP TABLE IF EXISTS `visit_photo_face_scans`;
CREATE TABLE IF NOT EXISTS `visit_photo_face_scans` (
  `face_scan_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `visit_photo_id` bigint unsigned NOT NULL,
  `visit_request_id` bigint unsigned NOT NULL,
  `visit_instance_id` bigint unsigned NOT NULL,
  `file_id` bigint unsigned NOT NULL,
  `api_config_id` bigint unsigned NOT NULL,
  `status` enum('PENDING','PROCESSING','SUCCEEDED','FAILED','CONFIRMED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING',
  `provider_name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'GOOGLE_CLOUD_VISION',
  `provider_request_id` varchar(255) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `feature_type` varchar(80) COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'FACE_DETECTION',
  `image_width` int unsigned DEFAULT NULL,
  `image_height` int unsigned DEFAULT NULL,
  `detected_face_count` int unsigned NOT NULL DEFAULT '0',
  `reviewed_face_count` int unsigned NOT NULL DEFAULT '0',
  `ignored_face_count` int unsigned NOT NULL DEFAULT '0',
  `error_code` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `error_message` text COLLATE utf8mb4_unicode_ci,
  `requested_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `requested_by` bigint unsigned DEFAULT NULL,
  `completed_at` datetime DEFAULT NULL,
  `confirmed_at` datetime DEFAULT NULL,
  `confirmed_by` bigint unsigned DEFAULT NULL,
  `row_version` int unsigned NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (`face_scan_id`),
  UNIQUE KEY `uq_visit_photo_face_scans_scope` (`face_scan_id`,`visit_request_id`,`visit_instance_id`,`file_id`),
  KEY `idx_visit_photo_face_scans_photo_time` (`visit_photo_id`,`created_at`),
  KEY `idx_visit_photo_face_scans_instance_time` (`visit_instance_id`,`created_at`),
  KEY `idx_visit_photo_face_scans_request_time` (`visit_request_id`,`created_at`),
  KEY `idx_visit_photo_face_scans_status_time` (`status`,`created_at`),
  KEY `idx_visit_photo_face_scans_api_time` (`api_config_id`,`created_at`),
  KEY `idx_visit_photo_face_scans_requested_by` (`requested_by`,`requested_at`),
  KEY `idx_visit_photo_face_scans_confirmed_by` (`confirmed_by`,`confirmed_at`),
  KEY `fk_visit_photo_face_scans_photo_scope` (`visit_photo_id`,`visit_request_id`,`visit_instance_id`,`file_id`),
  CONSTRAINT `fk_visit_photo_face_scans_api_config` FOREIGN KEY (`api_config_id`) REFERENCES `api_configurations` (`api_config_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_photo_face_scans_confirmed_by` FOREIGN KEY (`confirmed_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_photo_face_scans_photo_scope` FOREIGN KEY (`visit_photo_id`, `visit_request_id`, `visit_instance_id`, `file_id`) REFERENCES `visit_photos` (`visit_photo_id`, `visit_request_id`, `visit_instance_id`, `file_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_photo_face_scans_requested_by` FOREIGN KEY (`requested_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `ck_visit_photo_face_scans_counts` CHECK (((`reviewed_face_count` + `ignored_face_count`) <= `detected_face_count`)),
  CONSTRAINT `ck_visit_photo_face_scans_dimensions` CHECK ((((`image_width` is null) and (`image_height` is null)) or ((`image_width` > 0) and (`image_height` > 0)))),
  CONSTRAINT `ck_visit_photo_face_scans_feature` CHECK ((`feature_type` = _utf8mb4'FACE_DETECTION'))
) ENGINE=InnoDB AUTO_INCREMENT=13 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='One Google Cloud Vision FACE_DETECTION execution for one private visit photo. Stores scan state/counts only; no face embedding or automatic identity recognition.';

-- Dumping data for table pems_db.visit_photo_face_scans: ~12 rows (approximately)

-- Dumping structure for table pems_db.visit_photo_folders
DROP TABLE IF EXISTS `visit_photo_folders`;
CREATE TABLE IF NOT EXISTS `visit_photo_folders` (
  `visit_photo_folder_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `visit_request_id` bigint unsigned NOT NULL,
  `storage_provider` enum('GOOGLE_DRIVE') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'GOOGLE_DRIVE',
  `external_folder_id` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Google Drive folder id below configured VisitRequestPhotoFolderId root',
  `folder_name` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Stable application-generated name, recommended: VR-{visit_request_id or request_code}',
  `web_view_url` varchar(700) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `status` enum('ACTIVE','ARCHIVED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`visit_photo_folder_id`),
  UNIQUE KEY `uq_visit_photo_folders_request` (`visit_request_id`),
  UNIQUE KEY `uq_visit_photo_folders_external` (`external_folder_id`),
  UNIQUE KEY `uq_visit_photo_folders_folder_request` (`visit_photo_folder_id`,`visit_request_id`),
  KEY `idx_visit_photo_folders_status` (`status`,`created_at`),
  KEY `idx_visit_photo_folders_created_by` (`created_by`,`created_at`),
  KEY `fk_visit_photo_folders_updated_by` (`updated_by`),
  CONSTRAINT `fk_visit_photo_folders_created_by` FOREIGN KEY (`created_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_photo_folders_request` FOREIGN KEY (`visit_request_id`) REFERENCES `visit_requests` (`visit_request_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_photo_folders_updated_by` FOREIGN KEY (`updated_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `visit_photo_folders_chk_1` CHECK ((trim(`external_folder_id`) <> _utf8mb4'')),
  CONSTRAINT `visit_photo_folders_chk_2` CHECK ((trim(`folder_name`) <> _utf8mb4''))
) ENGINE=InnoDB AUTO_INCREMENT=19 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='One private Google Drive child folder per visit request/delegation; root folder id is application configuration, not Gallery data.';

-- Dumping data for table pems_db.visit_photo_folders: ~18 rows (approximately)

-- Dumping structure for table pems_db.visit_photos
DROP TABLE IF EXISTS `visit_photos`;
CREATE TABLE IF NOT EXISTS `visit_photos` (
  `visit_photo_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `visit_request_id` bigint unsigned NOT NULL,
  `visit_instance_id` bigint unsigned NOT NULL,
  `visit_photo_folder_id` bigint unsigned NOT NULL,
  `file_id` bigint unsigned NOT NULL,
  `caption` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `taken_at` datetime DEFAULT NULL,
  `status` enum('ACTIVE','REMOVED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'ACTIVE',
  `uploaded_by` bigint unsigned NOT NULL COMMENT 'Student user who uploaded the image; validated against ACCEPTED STUDENT participation by trigger and backend',
  `uploaded_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `removed_at` datetime DEFAULT NULL,
  `removed_by` bigint unsigned DEFAULT NULL,
  `removal_reason` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`visit_photo_id`),
  UNIQUE KEY `uq_visit_photos_file` (`file_id`),
  UNIQUE KEY `uq_visit_photos_face_scan_scope` (`visit_photo_id`,`visit_request_id`,`visit_instance_id`,`file_id`),
  KEY `idx_visit_photos_request_time` (`visit_request_id`,`uploaded_at`),
  KEY `idx_visit_photos_instance_time` (`visit_instance_id`,`uploaded_at`),
  KEY `idx_visit_photos_folder_time` (`visit_photo_folder_id`,`uploaded_at`),
  KEY `idx_visit_photos_uploader_time` (`uploaded_by`,`uploaded_at`),
  KEY `idx_visit_photos_status_time` (`status`,`uploaded_at`),
  KEY `fk_visit_photos_request_instance` (`visit_request_id`,`visit_instance_id`),
  KEY `fk_visit_photos_folder_request` (`visit_photo_folder_id`,`visit_request_id`),
  KEY `fk_visit_photos_removed_by` (`removed_by`),
  CONSTRAINT `fk_visit_photos_file` FOREIGN KEY (`file_id`) REFERENCES `files` (`file_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_photos_folder_request` FOREIGN KEY (`visit_photo_folder_id`, `visit_request_id`) REFERENCES `visit_photo_folders` (`visit_photo_folder_id`, `visit_request_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_photos_removed_by` FOREIGN KEY (`removed_by`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_photos_request_instance` FOREIGN KEY (`visit_request_id`, `visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_request_id`, `visit_instance_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_photos_uploaded_by` FOREIGN KEY (`uploaded_by`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `visit_photos_chk_1` CHECK (((`caption` is null) or (trim(`caption`) <> _utf8mb4'')))
) ENGINE=InnoDB AUTO_INCREMENT=71 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Private visit/delegation photos uploaded by an ACCEPTED Student participant. Binary is on Drive and metadata is in files; independent from Gallery.';

-- Dumping data for table pems_db.visit_photos: ~70 rows (approximately)

-- Dumping structure for table pems_db.visit_request_campuses
DROP TABLE IF EXISTS `visit_request_campuses`;
CREATE TABLE IF NOT EXISTS `visit_request_campuses` (
  `visit_instance_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `visit_request_id` bigint unsigned NOT NULL,
  `campus_id` bigint unsigned NOT NULL,
  `planned_start_at` datetime NOT NULL COMMENT 'Ngày giờ bắt đầu dự kiến tại campus',
  `planned_end_at` datetime NOT NULL COMMENT 'Ngày giờ kết thúc dự kiến tại campus',
  `status` enum('WAITING_CONTACT_CONFIRMATION','WAITING_REQUEST_APPROVAL','ASSIGNED','BEFORE_VISIT','DURING_VISIT','AFTER_VISIT','CLOSED','CANCELLED','REJECTED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'WAITING_CONTACT_CONFIRMATION' COMMENT 'WAITING_CONTACT_CONFIRMATION=đầu mối vận hành campus chưa xác nhận; WAITING_REQUEST_APPROVAL=đã có đầu mối, chờ Staff Leader campus xử lý; ASSIGNED=Staff Leader đã duyệt và gán Host trong cùng transaction, Host CHƯA bắt đầu chuẩn bị; BEFORE_VISIT=Host đã bấm Bắt đầu chuẩn bị, các thao tác setup mới mở; REJECTED=Staff Leader campus từ chối tiếp nhận.',
  `operational_contact_user_id` bigint unsigned DEFAULT NULL COMMENT 'Tài khoản đầu mối vận hành đã xác nhận cho ĐÚNG campus này. NULL khi chưa xác nhận (status=WAITING_CONTACT_CONFIRMATION). Tự khớp registrant thì set ngay lúc submit, không gửi email.',
  `operational_contact_confirmed_at` datetime DEFAULT NULL COMMENT 'Thời điểm đầu mối vận hành được liên kết (giờ VN)',
  `operational_contact_confirmation_source` enum('REGISTRANT_SELF_MATCH','EMAIL_CONFIRMATION','TRANSFER') COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'REGISTRANT_SELF_MATCH=email trùng registrant đã xác thực, auto-link không gửi mail; EMAIL_CONFIRMATION=accept qua link; TRANSFER=chuyển đầu mối sau khi đã có quyết định campus',
  `coordinator_user_id` bigint unsigned DEFAULT NULL COMMENT 'Staff Leader điều phối campus instance; được route theo campus ngay sau khi submit.',
  `coordinator_assigned_by` bigint unsigned DEFAULT NULL COMMENT 'Người/tiến trình route coordinator; không còn là bước Staff Leader xử lý multi-campus',
  `coordinator_assigned_at` datetime DEFAULT NULL COMMENT 'Thời điểm gán coordinator',
  `current_host_user_id` bigint unsigned DEFAULT NULL COMMENT 'Host chính thức của campus instance. Set một lần tại thời điểm Staff Leader approve; có thể là IC Staff hoặc chính Staff Leader đang duyệt.',
  `host_assigned_by` bigint unsigned DEFAULT NULL COMMENT 'Staff Leader approve và gán host chính thức',
  `host_assigned_at` datetime DEFAULT NULL COMMENT 'Thời điểm host chính thức được gán',
  `host_selection_mode` enum('SELF','SELECTED','WAIT_FOR_LATER') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'WAIT_FOR_LATER' COMMENT 'Phương án người phụ trách tiếp đón do người tạo chọn: SELF=tự nhận; SELECTED=Staff Leader chỉ định IC Staff cùng campus; WAIT_FOR_LATER=chờ Staff Leader phân công sau khi cổng mở. KHÔNG phải lifecycle status.',
  `proposed_host_user_id` bigint unsigned DEFAULT NULL COMMENT 'Host dự kiến của campus này. NULL bắt buộc khi host_selection_mode=WAIT_FOR_LATER. Không bao giờ mang nghĩa đã phân công — xem current_host_user_id.',
  `proposed_host_by_user_id` bigint unsigned DEFAULT NULL COMMENT 'Người đề xuất Host dự kiến (Staff Leader hoặc chính IC Staff tự nhận). Là actor được preauthorize; khi kích hoạt sẽ trở thành decided_by.',
  `proposed_host_at` datetime DEFAULT NULL COMMENT 'Thời điểm lưu/cập nhật đề xuất Host dự kiến (giờ VN)',
  `proposed_host_activation_status` enum('PENDING','ACTIVATED','NEEDS_RESELECTION') COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'PENDING=đã lưu, chờ cổng xác nhận mở; ACTIVATED=đã kích hoạt thành current host; NEEDS_RESELECTION=cổng đã mở nhưng proposal không còn hợp lệ, Staff Leader phải chọn lại. NULL khi WAIT_FOR_LATER.',
  `proposed_host_activated_at` datetime DEFAULT NULL COMMENT 'Thời điểm proposal được kích hoạt thành Host chính thức; dùng làm bằng chứng idempotency khi confirmation bị replay.',
  `decided_by` bigint unsigned DEFAULT NULL COMMENT 'Người xử lý campus instance (Staff Leader duyệt/từ chối, hoặc IC Staff tự nhận host trong transaction tạo đơn của chính mình)',
  `decided_at` datetime DEFAULT NULL COMMENT 'Thời điểm campus instance được xử lý',
  `decision_actor_role` enum('STAFF_LEADER','STAFF') COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'STAFF_LEADER = duyệt chuẩn/gán host/leader self-host, hoặc kích hoạt proposal của Leader; STAFF = IC Staff thường tự nhận host qua Host dự kiến trên đơn chính mình đăng ký (decision_source=PREAUTHORIZED_HOST_ACTIVATION).',
  `decision_source` enum('STANDARD_CAMPUS_REVIEW','PREAUTHORIZED_HOST_ACTIVATION') COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Nguồn quyết định: STANDARD_CAMPUS_REVIEW=Staff Leader duyệt instance pending sau khi cổng mở; PREAUTHORIZED_HOST_ACTIVATION=Host dự kiến đã được preauthorize lúc tạo/sửa đơn, được revalidate và kích hoạt tại đúng thời điểm cổng xác nhận đầu mối mở. Hai nguồn cũ INTERNAL_SELF_HOST/INTERNAL_LEADER_ASSIGN đã bỏ cùng direct processing: chúng gán Host TRƯỚC cổng.',
  `decision_note` text COLLATE utf8mb4_unicode_ci COMMENT 'Ghi chú duyệt hoặc lý do từ chối; REJECTED bắt buộc có lý do',
  `closed_by` bigint unsigned DEFAULT NULL,
  `closed_at` datetime DEFAULT NULL,
  `close_note` text COLLATE utf8mb4_unicode_ci,
  `news_not_required` tinyint(1) NOT NULL DEFAULT '0' COMMENT 'Host xác nhận chuyến này không cần tạo/duyệt bài tin tức; backend dùng khi đóng đoàn: có news PUBLISHED hoặc news_not_required = 1',
  `cancelled_by` bigint unsigned DEFAULT NULL COMMENT 'Visitor hoặc Host thực hiện hủy campus instance',
  `cancelled_at` datetime DEFAULT NULL COMMENT 'Thời điểm hủy campus instance',
  `cancellation_actor_type` enum('VISITOR','HOST') COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'VISITOR=khách tự hủy; HOST=Staff được gán làm host hủy thay khách theo xác nhận ngoài hệ thống',
  `cancellation_source` enum('SELF_SERVICE','EXTERNAL_CONFIRMATION') COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'SELF_SERVICE=Visitor tự hủy; EXTERNAL_CONFIRMATION=Host hủy sau khi khách xác nhận ngoài hệ thống',
  `cancellation_reason` text COLLATE utf8mb4_unicode_ci COMMENT 'Lý do hủy; nếu EXTERNAL_CONFIRMATION thì ghi rõ kênh xác nhận, thời điểm, người xác nhận và lý do.',
  `preparation_note` text COLLATE utf8mb4_unicode_ci COMMENT 'Ghi chú chuẩn bị nội bộ của Host cho campus visit instance',
  `row_version` int unsigned NOT NULL DEFAULT '0' COMMENT 'Optimistic concurrency token',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`visit_instance_id`),
  UNIQUE KEY `uq_visit_instance_request_campus` (`visit_request_id`,`campus_id`),
  UNIQUE KEY `uq_vrc_request_instance` (`visit_request_id`,`visit_instance_id`),
  KEY `idx_visit_instances_campus_status_time` (`campus_id`,`status`,`planned_start_at`),
  KEY `idx_visit_instances_request` (`visit_request_id`),
  KEY `idx_visit_instances_status_time` (`status`,`planned_start_at`),
  KEY `idx_visit_instances_coordinator` (`coordinator_user_id`,`status`),
  KEY `idx_visit_instances_coordinator_assigned` (`coordinator_assigned_by`,`coordinator_assigned_at`),
  KEY `idx_visit_instances_current_host` (`current_host_user_id`,`status`),
  KEY `idx_visit_instances_host_assigned` (`host_assigned_by`,`host_assigned_at`),
  KEY `idx_visit_instances_decision` (`decided_by`,`decided_at`),
  KEY `idx_visit_instances_decision_role` (`decision_actor_role`,`decided_at`),
  KEY `idx_visit_instances_decision_source` (`decision_source`,`decided_at`),
  KEY `idx_visit_instances_cancelled` (`cancelled_by`,`cancelled_at`),
  KEY `idx_visit_instances_cancel_actor` (`cancellation_actor_type`,`cancelled_at`),
  KEY `idx_visit_instances_visibility_campus_request` (`campus_id`,`visit_request_id`,`status`,`current_host_user_id`),
  KEY `idx_visit_instances_op_contact` (`operational_contact_user_id`,`visit_instance_id`),
  KEY `idx_visit_instances_leader_queue` (`campus_id`,`status`,`visit_request_id`),
  KEY `idx_visit_instances_proposed_host` (`proposed_host_user_id`,`proposed_host_activation_status`),
  KEY `fk_visit_instances_proposed_host_by` (`proposed_host_by_user_id`),
  KEY `fk_visit_instances_closed_by` (`closed_by`),
  CONSTRAINT `fk_visit_instances_campus` FOREIGN KEY (`campus_id`) REFERENCES `campuses` (`campus_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_instances_cancelled_by` FOREIGN KEY (`cancelled_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_instances_closed_by` FOREIGN KEY (`closed_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_instances_coordinator` FOREIGN KEY (`coordinator_user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_instances_coordinator_assigned_by` FOREIGN KEY (`coordinator_assigned_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_instances_current_host` FOREIGN KEY (`current_host_user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_instances_decided_by` FOREIGN KEY (`decided_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_instances_host_assigned_by` FOREIGN KEY (`host_assigned_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_instances_operational_contact` FOREIGN KEY (`operational_contact_user_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_visit_instances_proposed_host` FOREIGN KEY (`proposed_host_user_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_visit_instances_proposed_host_by` FOREIGN KEY (`proposed_host_by_user_id`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `fk_visit_instances_request` FOREIGN KEY (`visit_request_id`) REFERENCES `visit_requests` (`visit_request_id`) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT `ck_visit_instance_min_duration_30m` CHECK ((timestampdiff(MINUTE,`planned_start_at`,`planned_end_at`) >= 30)),
  CONSTRAINT `ck_vrc_proposed_host_activated_at` CHECK ((((`proposed_host_activation_status` = _utf8mb4'ACTIVATED') and (`proposed_host_activated_at` is not null)) or ((coalesce(`proposed_host_activation_status`,_utf8mb4'') <> _utf8mb4'ACTIVATED') and (`proposed_host_activated_at` is null)))),
  CONSTRAINT `ck_vrc_proposed_host_meta` CHECK ((((`proposed_host_user_id` is null) and (`proposed_host_by_user_id` is null) and (`proposed_host_at` is null) and (`proposed_host_activation_status` is null) and (`proposed_host_activated_at` is null)) or ((`proposed_host_user_id` is not null) and (`proposed_host_by_user_id` is not null) and (`proposed_host_at` is not null) and (`proposed_host_activation_status` is not null)))),
  CONSTRAINT `ck_vrc_proposed_host_mode` CHECK ((((`host_selection_mode` = _utf8mb4'WAIT_FOR_LATER') and (`proposed_host_user_id` is null)) or ((`host_selection_mode` in (_utf8mb4'SELF',_utf8mb4'SELECTED')) and (`proposed_host_user_id` is not null)))),
  CONSTRAINT `visit_request_campuses_chk_1` CHECK ((`planned_end_at` > `planned_start_at`))
) ENGINE=InnoDB AUTO_INCREMENT=47153 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Mỗi campus trong request có một instance riêng, mang đầu mối vận hành + Host dự kiến + quyết định riêng. Staff Leader campus xử lý approve/reject độc lập và CHỈ sau khi mọi đầu mối của request đã xác nhận. Hai đường tới ASSIGNED: (a) Staff Leader duyệt + gán Host (STANDARD_CAMPUS_REVIEW), (b) Host dự kiến đã preauthorize được revalidate và kích hoạt ngay khi cổng mở (PREAUTHORIZED_HOST_ACTIVATION). ASSIGNED -> BEFORE_VISIT vẫn phải do Host bấm Bắt đầu chuẩn bị.';

-- Dumping data for table pems_db.visit_request_campuses: ~127 rows (approximately)

-- Dumping structure for table pems_db.visit_request_fingerprint_guards
DROP TABLE IF EXISTS `visit_request_fingerprint_guards`;
CREATE TABLE IF NOT EXISTS `visit_request_fingerprint_guards` (
  `fingerprint` varchar(64) COLLATE utf8mb4_unicode_ci NOT NULL,
  `created_at` datetime NOT NULL,
  `updated_at` datetime NOT NULL,
  PRIMARY KEY (`fingerprint`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Row-level lock table for 15-minute duplicate visit-request guard. INSERT IGNORE + SELECT … FOR UPDATE sequences concurrent submits.';

-- Dumping data for table pems_db.visit_request_fingerprint_guards: ~16 rows (approximately)

-- Dumping structure for table pems_db.visit_request_identity_change_events
DROP TABLE IF EXISTS `visit_request_identity_change_events`;
CREATE TABLE IF NOT EXISTS `visit_request_identity_change_events` (
  `identity_change_event_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `identity_change_id` bigint unsigned NOT NULL,
  `visit_request_id` bigint unsigned NOT NULL,
  `visit_instance_id` bigint unsigned NOT NULL COMMENT 'Campus của sự kiện; audit luôn truy được về đúng cơ sở',
  `event_type` varchar(80) COLLATE utf8mb4_unicode_ci NOT NULL,
  `from_status` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `to_status` varchar(30) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `actor_user_id` bigint unsigned DEFAULT NULL,
  `email_masked` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `reason` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `correlation_id` varchar(100) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`identity_change_event_id`),
  KEY `idx_ice_change` (`identity_change_id`,`created_at`),
  KEY `idx_ice_request` (`visit_request_id`,`created_at`),
  KEY `idx_ice_instance` (`visit_instance_id`,`created_at`),
  KEY `idx_ice_correlation` (`correlation_id`),
  KEY `fk_ice_actor` (`actor_user_id`),
  CONSTRAINT `fk_ice_actor` FOREIGN KEY (`actor_user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_ice_change` FOREIGN KEY (`identity_change_id`) REFERENCES `visit_request_identity_changes` (`identity_change_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=34 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Append-only: mọi transition identity-change. Không update/delete ngoài retention job.';

-- Dumping data for table pems_db.visit_request_identity_change_events: ~33 rows (approximately)

-- Dumping structure for table pems_db.visit_request_identity_changes
DROP TABLE IF EXISTS `visit_request_identity_changes`;
CREATE TABLE IF NOT EXISTS `visit_request_identity_changes` (
  `identity_change_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `visit_request_id` bigint unsigned NOT NULL,
  `visit_instance_id` bigint unsigned NOT NULL COMMENT 'Campus mà lời mời này thuộc về. Một email phụ trách N campus vẫn có N row riêng.',
  `change_kind` enum('INITIAL_CONFIRMATION','TRANSFER') COLLATE utf8mb4_unicode_ci NOT NULL,
  `token_version` int unsigned NOT NULL DEFAULT '1' COMMENT 'Tăng mỗi lần resend. Dùng trong dedupe key OP_CONTACT_CONFIRM:{id}:{tokenVersion} và để supersede token cũ.',
  `confirmation_method` enum('GOOGLE_SSO','OTP_FALLBACK') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'GOOGLE_SSO',
  `old_user_id` bigint unsigned DEFAULT NULL,
  `new_user_id` bigint unsigned DEFAULT NULL,
  `old_email_normalized` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `new_email_normalized` varchar(150) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'Required while PENDING; NULL after 90-day retention redaction',
  `new_email_masked` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL,
  `pending_snapshot_json` json DEFAULT NULL,
  `status` enum('PENDING','APPLIED','DECLINED','EXPIRED','CANCELLED','SUPERSEDED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING',
  `expected_request_row_version` int unsigned NOT NULL,
  `requested_by` bigint unsigned NOT NULL,
  `requested_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `expires_at` datetime NOT NULL,
  `applied_at` datetime DEFAULT NULL,
  `declined_at` datetime DEFAULT NULL,
  `cancelled_at` datetime DEFAULT NULL,
  `superseded_at` datetime DEFAULT NULL,
  `retention_until` datetime DEFAULT NULL,
  `redacted_at` datetime DEFAULT NULL,
  `reason` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `resend_count` int unsigned NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `pending_guard` varchar(80) COLLATE utf8mb4_unicode_ci GENERATED ALWAYS AS ((case when (`status` = _utf8mb4'PENDING') then concat(_utf8mb4'I:',`visit_instance_id`) else NULL end)) VIRTUAL,
  PRIMARY KEY (`identity_change_id`),
  UNIQUE KEY `uq_identity_change_pending` (`pending_guard`),
  KEY `idx_identity_change_instance_status` (`visit_instance_id`,`status`),
  KEY `idx_identity_change_request_status` (`visit_request_id`,`status`),
  KEY `idx_identity_change_status_expires` (`status`,`expires_at`),
  KEY `idx_identity_change_retention` (`status`,`retention_until`),
  KEY `idx_identity_change_new_email` (`new_email_normalized`),
  KEY `fk_identity_change_instance` (`visit_request_id`,`visit_instance_id`),
  KEY `fk_identity_change_old_user` (`old_user_id`),
  KEY `fk_identity_change_new_user` (`new_user_id`),
  KEY `fk_identity_change_requested_by` (`requested_by`),
  CONSTRAINT `fk_identity_change_instance` FOREIGN KEY (`visit_request_id`, `visit_instance_id`) REFERENCES `visit_request_campuses` (`visit_request_id`, `visit_instance_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `fk_identity_change_new_user` FOREIGN KEY (`new_user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_identity_change_old_user` FOREIGN KEY (`old_user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_identity_change_requested_by` FOREIGN KEY (`requested_by`) REFERENCES `users` (`user_id`) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=67021 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Trạng thái xác nhận/chuyển đầu mối vận hành THEO TỪNG CAMPUS. visit_request_campuses.operational_contact_user_id chỉ được set trong đúng transaction chuyển row này sang APPLIED.';

-- Dumping data for table pems_db.visit_request_identity_changes: ~20 rows (approximately)

-- Dumping structure for table pems_db.visit_request_pending_forms
DROP TABLE IF EXISTS `visit_request_pending_forms`;
CREATE TABLE IF NOT EXISTS `visit_request_pending_forms` (
  `pending_form_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `submission_id` varchar(36) COLLATE utf8mb4_unicode_ci NOT NULL,
  `registrant_email` varchar(255) COLLATE utf8mb4_unicode_ci NOT NULL,
  `fingerprint_v2` char(64) COLLATE utf8mb4_unicode_ci NOT NULL,
  `snapshot_json` longtext COLLATE utf8mb4_unicode_ci NOT NULL,
  `created_at` datetime NOT NULL,
  `expires_at` datetime NOT NULL,
  `consumed_at` datetime DEFAULT NULL,
  PRIMARY KEY (`pending_form_id`),
  UNIQUE KEY `uq_pending_forms_submission` (`submission_id`),
  KEY `idx_pending_forms_expires` (`expires_at`)
) ENGINE=InnoDB AUTO_INCREMENT=19 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Bind snapshot v2 vào submit intent lúc initiate; verify tạo request từ snapshot đã bind. Consumed at verify.';

-- Dumping data for table pems_db.visit_request_pending_forms: ~18 rows (approximately)

-- Dumping structure for table pems_db.visit_request_revision_history
DROP TABLE IF EXISTS `visit_request_revision_history`;
CREATE TABLE IF NOT EXISTS `visit_request_revision_history` (
  `request_revision_history_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `visit_request_id` bigint unsigned NOT NULL,
  `request_revision` int unsigned NOT NULL,
  `source_type` enum('CREATE','SAFE_EDIT','PENDING_EDIT','MIGRATION','RESUBMIT') COLLATE utf8mb4_unicode_ci NOT NULL,
  `source_id` bigint unsigned DEFAULT NULL,
  `snapshot_json` json NOT NULL,
  `applied_by` bigint unsigned DEFAULT NULL,
  `applied_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `reason` varchar(500) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  PRIMARY KEY (`request_revision_history_id`),
  UNIQUE KEY `uq_vrrh_request_revision` (`visit_request_id`,`request_revision`),
  KEY `idx_vrrh_request_time` (`visit_request_id`,`applied_at`),
  KEY `fk_vrrh_applied_by` (`applied_by`),
  CONSTRAINT `fk_vrrh_applied_by` FOREIGN KEY (`applied_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_vrrh_request` FOREIGN KEY (`visit_request_id`) REFERENCES `visit_requests` (`visit_request_id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=46258 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='Lịch sử snapshot cấp request (display fields). Quan hệ email/account lấy identity-change events làm lịch sử chính.';

-- Dumping data for table pems_db.visit_request_revision_history: ~84 rows (approximately)

-- Dumping structure for table pems_db.visit_requests
DROP TABLE IF EXISTS `visit_requests`;
CREATE TABLE IF NOT EXISTS `visit_requests` (
  `visit_request_id` bigint unsigned NOT NULL AUTO_INCREMENT,
  `request_code` varchar(50) COLLATE utf8mb4_unicode_ci NOT NULL,
  `submission_id` char(36) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'UUID idempotency cho một submit intent',
  `business_fingerprint` char(64) COLLATE utf8mb4_unicode_ci DEFAULT NULL COMMENT 'SHA-256 fingerprint V2 của core visit identity; non-unique để cho phép gửi lại hợp lệ',
  `registrant_user_id` bigint unsigned DEFAULT NULL COMMENT 'Tài khoản NGƯỜI ĐĂNG KÝ (submitter) — chủ sở hữu DUY NHẤT ở cấp request: xem toàn bộ campus, sửa phần request-level, đổi đầu mối, hủy toàn request theo rule. Có thể là VISITOR, STAFF hoặc STAFF LEADER; role người tạo KHÔNG bypass cổng xác nhận đầu mối. Quyền vận hành từng campus thuộc visit_request_campuses.operational_contact_user_id.',
  `partner_id` bigint unsigned DEFAULT NULL,
  `created_source` enum('VISITOR_SUBMITTED','STAFF_CREATED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'VISITOR_SUBMITTED',
  `has_mixed_campus_details` tinyint(1) NOT NULL DEFAULT '0' COMMENT 'Backend-derived: 1 when campus detail snapshots differ. Never accepted from the client.',
  `registrant_full_name` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Họ và tên người đăng ký',
  `registrant_organization` varchar(200) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Đơn vị công tác người đăng ký',
  `registrant_job_title` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Chức danh/phòng ban người đăng ký',
  `registrant_phone` varchar(50) COLLATE utf8mb4_unicode_ci DEFAULT NULL,
  `registrant_email` varchar(150) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Email người đăng ký',
  `registrant_nationality` varchar(100) COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'Quốc tịch người đăng ký',
  `visit_scope` enum('SINGLE_CAMPUS','MULTI_CAMPUS') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'SINGLE_CAMPUS' COMMENT 'SINGLE_CAMPUS/MULTI_CAMPUS mô tả số campus được chọn; từng campus instance được route độc lập.',
  `contact_gate_revision` int unsigned NOT NULL DEFAULT '0' COMMENT 'Số hiệu lần mở cổng xác nhận đầu mối; dùng làm dedupe key cho notification duyệt',
  `status` enum('PENDING_CONTACT_CONFIRMATION','PENDING_APPROVAL','PARTIALLY_APPROVED','APPROVED','REJECTED','CANCELLED') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'PENDING_CONTACT_CONFIRMATION' COMMENT 'Aggregate request status. PENDING_CONTACT_CONFIRMATION=còn campus chưa có đầu mối xác nhận (Staff Leader chưa thấy đơn); các trạng thái sau derive từ quyết định từng campus.',
  `submitted_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `email_verified_at` datetime DEFAULT NULL,
  `resubmission_count` int unsigned NOT NULL DEFAULT '0' COMMENT 'Số lần Visitor sửa và gửi lại request sau khi toàn bộ campus bị từ chối',
  `last_resubmitted_at` datetime DEFAULT NULL COMMENT 'Thời điểm gần nhất Visitor gửi lại request sau khi bị từ chối',
  `last_resubmitted_by` bigint unsigned DEFAULT NULL COMMENT 'Visitor gần nhất gửi lại request sau khi bị từ chối',
  `cancelled_by` bigint unsigned DEFAULT NULL COMMENT 'Visitor hủy toàn bộ request/delegation',
  `cancelled_at` datetime DEFAULT NULL COMMENT 'Thời điểm visitor hủy toàn bộ request/delegation',
  `cancellation_reason` text COLLATE utf8mb4_unicode_ci COMMENT 'Lý do hủy toàn bộ request/delegation. Người hủy = người đăng ký (registrant_user_id). Trigger trg_visit_requests_cancel_validate_bu enforce actor + guard 24h/started-campus.',
  `row_version` int unsigned NOT NULL DEFAULT '0' COMMENT 'Optimistic concurrency token',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by` bigint unsigned DEFAULT NULL,
  `updated_at` datetime DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  `updated_by` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`visit_request_id`),
  UNIQUE KEY `uq_visit_requests_code` (`request_code`),
  UNIQUE KEY `uq_visit_requests_submission_id` (`submission_id`),
  KEY `idx_visit_requests_fingerprint_time_status` (`business_fingerprint`,`submitted_at`,`status`),
  KEY `idx_visit_requests_registrant_user` (`registrant_user_id`,`submitted_at`),
  KEY `idx_visit_requests_partner` (`partner_id`),
  KEY `idx_visit_requests_status_submitted` (`status`,`submitted_at`),
  KEY `idx_visit_requests_registrant_email` (`registrant_email`),
  KEY `idx_visit_requests_scope_status` (`visit_scope`,`status`),
  KEY `idx_visit_requests_scope_status_submitted` (`visit_scope`,`status`,`submitted_at`),
  KEY `idx_visit_requests_created_source` (`created_source`),
  KEY `idx_visit_requests_mixed_details` (`has_mixed_campus_details`,`status`,`submitted_at`),
  KEY `idx_visit_requests_gate` (`status`,`contact_gate_revision`),
  KEY `idx_visit_requests_cancelled` (`cancelled_by`,`cancelled_at`),
  KEY `idx_visit_requests_resubmission` (`resubmission_count`,`last_resubmitted_at`),
  KEY `idx_visit_requests_last_resubmitted_by` (`last_resubmitted_by`,`last_resubmitted_at`),
  FULLTEXT KEY `ft_visit_requests_frontend_search` (`request_code`,`registrant_full_name`,`registrant_organization`,`registrant_email`),
  CONSTRAINT `fk_visit_requests_cancelled_by` FOREIGN KEY (`cancelled_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_requests_last_resubmitted_by` FOREIGN KEY (`last_resubmitted_by`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_requests_partner` FOREIGN KEY (`partner_id`) REFERENCES `partners` (`partner_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `fk_visit_requests_registrant_user` FOREIGN KEY (`registrant_user_id`) REFERENCES `users` (`user_id`) ON DELETE SET NULL ON UPDATE CASCADE,
  CONSTRAINT `visit_requests_chk_1` CHECK ((trim(`registrant_job_title`) <> _utf8mb4'')),
  CONSTRAINT `visit_requests_chk_2` CHECK ((trim(`registrant_phone`) <> _utf8mb4'')),
  CONSTRAINT `visit_requests_chk_3` CHECK ((trim(`registrant_nationality`) <> _utf8mb4''))
) ENGINE=InnoDB AUTO_INCREMENT=47038 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='V2-only request parent: registrant snapshot, scope, aggregate status, cổng xác nhận đầu mối, idempotency và lifecycle metadata. KHÔNG có đầu mối cấp request — mọi form content và đầu mối vận hành nằm per campus.';

-- Dumping data for table pems_db.visit_requests: ~92 rows (approximately)

-- Dumping structure for trigger pems_db.trg_agenda_template_defaults_scope_bi
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_agenda_template_defaults_scope_bi` BEFORE INSERT ON `agenda_template_defaults` FOR EACH ROW BEGIN
  SET NEW.campus_scope_key = IFNULL(CAST(NEW.campus_id AS CHAR), 'GLOBAL');
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_agenda_template_defaults_scope_bu
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_agenda_template_defaults_scope_bu` BEFORE UPDATE ON `agenda_template_defaults` FOR EACH ROW BEGIN
  SET NEW.campus_scope_key = IFNULL(CAST(NEW.campus_id AS CHAR), 'GLOBAL');
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_agenda_templates_scope_bi
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_agenda_templates_scope_bi` BEFORE INSERT ON `agenda_templates` FOR EACH ROW BEGIN
  SET NEW.campus_scope_key = IFNULL(CAST(NEW.campus_id AS CHAR), 'GLOBAL');
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_agenda_templates_scope_bu
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_agenda_templates_scope_bu` BEFORE UPDATE ON `agenda_templates` FOR EACH ROW BEGIN
  SET NEW.campus_scope_key = IFNULL(CAST(NEW.campus_id AS CHAR), 'GLOBAL');
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_api_usage_quotas_scope_bi
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_api_usage_quotas_scope_bi` BEFORE INSERT ON `api_usage_quotas` FOR EACH ROW BEGIN
  SET NEW.campus_scope_key = IFNULL(CAST(NEW.campus_id AS CHAR), 'GLOBAL');
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_api_usage_quotas_scope_bu
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_api_usage_quotas_scope_bu` BEFORE UPDATE ON `api_usage_quotas` FOR EACH ROW BEGIN
  SET NEW.campus_scope_key = IFNULL(CAST(NEW.campus_id AS CHAR), 'GLOBAL');
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_auth_providers_validate_bi
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_auth_providers_validate_bi` BEFORE INSERT ON `user_auth_providers` FOR EACH ROW BEGIN
  IF NEW.provider_type IN ('GOOGLE_SSO','FEID')
     AND (NEW.provider_subject IS NULL OR NEW.provider_subject = '') THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'SSO/FEID provider_subject is required';
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_auth_providers_validate_bu
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_auth_providers_validate_bu` BEFORE UPDATE ON `user_auth_providers` FOR EACH ROW BEGIN
  IF NEW.provider_type IN ('GOOGLE_SSO','FEID')
     AND (NEW.provider_subject IS NULL OR NEW.provider_subject = '') THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'SSO/FEID provider_subject is required';
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_departments_one_ic_bi
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_departments_one_ic_bi` BEFORE INSERT ON `departments` FOR EACH ROW BEGIN
  DECLARE v_exists INT DEFAULT 0;

  IF NEW.department_type = 'IC' AND NEW.status = 'ACTIVE' THEN
    SELECT COUNT(*) INTO v_exists
    FROM departments
    WHERE campus_id = NEW.campus_id
      AND department_type = 'IC'
      AND status = 'ACTIVE';

    IF v_exists > 0 THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Each campus can have only one active IC department';
    END IF;
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_departments_one_ic_bu
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_departments_one_ic_bu` BEFORE UPDATE ON `departments` FOR EACH ROW BEGIN
  DECLARE v_exists INT DEFAULT 0;

  IF NEW.department_type = 'IC' AND NEW.status = 'ACTIVE' THEN
    SELECT COUNT(*) INTO v_exists
    FROM departments
    WHERE campus_id = NEW.campus_id
      AND department_type = 'IC'
      AND status = 'ACTIVE'
      AND department_id <> NEW.department_id;

    IF v_exists > 0 THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Each campus can have only one active IC department';
    END IF;
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_expense_reports_scope_bi
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_expense_reports_scope_bi` BEFORE INSERT ON `visit_expense_reports` FOR EACH ROW BEGIN
  DECLARE v_valid_logistics_owner INT DEFAULT 0;

  IF NEW.report_scope = 'GENERAL' THEN
    IF NEW.logistics_item_id IS NOT NULL OR NEW.department_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'GENERAL expense report must not have logistics_item_id or department_id';
    END IF;
    SET NEW.general_instance_guard = NEW.visit_instance_id;
    SET NEW.logistics_item_guard = NULL;
  ELSEIF NEW.report_scope = 'LOGISTICS' THEN
    IF NEW.logistics_item_id IS NULL OR NEW.department_id IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'LOGISTICS expense report requires logistics_item_id and department_id';
    END IF;
    SET NEW.general_instance_guard = NULL;
    SET NEW.logistics_item_guard = NEW.logistics_item_id;

    SELECT COUNT(*) INTO v_valid_logistics_owner
    FROM visit_logistics_items vli
    WHERE vli.logistics_item_id = NEW.logistics_item_id
      AND vli.visit_instance_id = NEW.visit_instance_id
      AND vli.requested_to_department_id = NEW.department_id;

    IF v_valid_logistics_owner = 0 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'LOGISTICS expense report must match the logistics item visit instance and requested department';
    END IF;
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_expense_reports_scope_bu
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_expense_reports_scope_bu` BEFORE UPDATE ON `visit_expense_reports` FOR EACH ROW BEGIN
  DECLARE v_valid_logistics_owner INT DEFAULT 0;

  IF NEW.report_scope = 'GENERAL' THEN
    IF NEW.logistics_item_id IS NOT NULL OR NEW.department_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'GENERAL expense report must not have logistics_item_id or department_id';
    END IF;
    SET NEW.general_instance_guard = NEW.visit_instance_id;
    SET NEW.logistics_item_guard = NULL;
  ELSEIF NEW.report_scope = 'LOGISTICS' THEN
    IF NEW.logistics_item_id IS NULL OR NEW.department_id IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'LOGISTICS expense report requires logistics_item_id and department_id';
    END IF;
    SET NEW.general_instance_guard = NULL;
    SET NEW.logistics_item_guard = NEW.logistics_item_id;

    SELECT COUNT(*) INTO v_valid_logistics_owner
    FROM visit_logistics_items vli
    WHERE vli.logistics_item_id = NEW.logistics_item_id
      AND vli.visit_instance_id = NEW.visit_instance_id
      AND vli.requested_to_department_id = NEW.department_id;

    IF v_valid_logistics_owner = 0 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'LOGISTICS expense report must match the logistics item visit instance and requested department';
    END IF;
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_feedbacks_not_self_bi
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_feedbacks_not_self_bi` BEFORE INSERT ON `feedbacks` FOR EACH ROW BEGIN
  IF NEW.target_user_id IS NOT NULL AND NEW.submitted_by_user_id = NEW.target_user_id THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Feedback submitter and target user cannot be the same';
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_feedbacks_not_self_bu
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_feedbacks_not_self_bu` BEFORE UPDATE ON `feedbacks` FOR EACH ROW BEGIN
  IF NEW.target_user_id IS NOT NULL AND NEW.submitted_by_user_id = NEW.target_user_id THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Feedback submitter and target user cannot be the same';
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_identity_changes_transfer_bi
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_identity_changes_transfer_bi` BEFORE INSERT ON `visit_request_identity_changes` FOR EACH ROW BEGIN
  IF NEW.change_kind = 'TRANSFER' AND NEW.old_user_id IS NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'TRANSFER identity change requires old_user_id (the current owner)';
  END IF;
  -- INITIAL_CONFIRMATION là lời mời đầu tiên của campus: chưa có chủ sở hữu nào để chuyển giao.
  IF NEW.change_kind = 'INITIAL_CONFIRMATION' AND NEW.old_user_id IS NOT NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'INITIAL_CONFIRMATION identity change must not carry old_user_id';
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_identity_changes_transfer_bu
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_identity_changes_transfer_bu` BEFORE UPDATE ON `visit_request_identity_changes` FOR EACH ROW BEGIN
  IF NEW.change_kind = 'TRANSFER' AND NEW.old_user_id IS NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'TRANSFER identity change requires old_user_id (the current owner)';
  END IF;
  -- INITIAL_CONFIRMATION là lời mời đầu tiên của campus: chưa có chủ sở hữu nào để chuyển giao.
  IF NEW.change_kind = 'INITIAL_CONFIRMATION' AND NEW.old_user_id IS NOT NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'INITIAL_CONFIRMATION identity change must not carry old_user_id';
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_sessions_validate_bi
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_sessions_validate_bi` BEFORE INSERT ON `user_sessions` FOR EACH ROW BEGIN
  DECLARE v_role_code VARCHAR(30);
  DECLARE v_primary_campus_id BIGINT UNSIGNED;

  SELECT r.role_code, u.primary_campus_id
    INTO v_role_code, v_primary_campus_id
  FROM users u
  JOIN roles r ON r.role_id = u.role_id
  WHERE u.user_id = NEW.user_id;

  IF NEW.login_portal = 'VISITOR' THEN
    IF v_role_code <> 'VISITOR' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only VISITOR can login via Visitor Portal';
    END IF;
    IF NEW.selected_campus_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Visitor Portal must not have selected_campus_id';
    END IF;
  ELSEIF NEW.login_portal = 'INTERNAL' THEN
    IF v_role_code = 'VISITOR' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'VISITOR cannot login via Internal Portal';
    END IF;
    IF v_primary_campus_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Internal user must have primary_campus_id';
    END IF;
    IF NEW.selected_campus_id IS NULL THEN
      SET NEW.selected_campus_id = v_primary_campus_id;
    ELSEIF NEW.selected_campus_id <> v_primary_campus_id THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Internal user can only login to their own primary campus';
    END IF;
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_users_protect_operational_contact_bu
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_users_protect_operational_contact_bu` BEFORE UPDATE ON `users` FOR EACH ROW BEGIN
  DECLARE v_linked_instance_count INT DEFAULT 0;

  IF NOT (NEW.status <=> OLD.status) AND NOT (NEW.status <=> 'ACTIVE') THEN
    SELECT COUNT(*)
      INTO v_linked_instance_count
    FROM visit_request_campuses vrc
    JOIN visit_requests vr ON vr.visit_request_id = vrc.visit_request_id
    WHERE vrc.operational_contact_user_id = OLD.user_id
      AND vrc.status NOT IN ('CANCELLED','REJECTED','CLOSED')
      AND vr.status <> 'CANCELLED';

    IF v_linked_instance_count > 0 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'LINKED_OPERATIONAL_CONTACT_CANNOT_BE_DEACTIVATED';
    END IF;
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_users_validate_bi
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_users_validate_bi` BEFORE INSERT ON `users` FOR EACH ROW BEGIN
  DECLARE v_role_code VARCHAR(30);
  DECLARE v_department_type VARCHAR(20);
  DECLARE v_department_campus_id BIGINT UNSIGNED;

  SELECT role_code INTO v_role_code
  FROM roles
  WHERE role_id = NEW.role_id
    AND status = 'ACTIVE';

  IF v_role_code IS NULL THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Invalid role_id';
  END IF;

  IF v_role_code = 'VISITOR' THEN
    IF NEW.sub_role IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'VISITOR must not have sub_role';
    END IF;
    IF NEW.department_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'VISITOR must not have department_id';
    END IF;
    IF NEW.primary_campus_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'VISITOR must not have primary_campus_id';
    END IF;
  ELSEIF v_role_code IN ('STAFF','DEPARTMENT') THEN
    IF NEW.sub_role IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF/DEPARTMENT must have sub_role';
    END IF;
    IF NEW.department_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF/DEPARTMENT must have department_id';
    END IF;

    SELECT department_type, campus_id
      INTO v_department_type, v_department_campus_id
    FROM departments
    WHERE department_id = NEW.department_id;

    IF v_role_code = 'STAFF' AND v_department_type <> 'IC' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF must belong to IC department';
    END IF;

    IF v_role_code = 'DEPARTMENT' AND v_department_type <> 'GENERAL' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'DEPARTMENT must belong to GENERAL department';
    END IF;

    IF NEW.primary_campus_id IS NULL THEN
      SET NEW.primary_campus_id = v_department_campus_id;
    ELSEIF NEW.primary_campus_id <> v_department_campus_id THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'primary_campus_id must match department campus';
    END IF;
  ELSE
    IF NEW.sub_role IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only STAFF/DEPARTMENT may have sub_role';
    END IF;
    IF NEW.department_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only STAFF/DEPARTMENT may have department_id';
    END IF;
    IF NEW.primary_campus_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Internal user must have primary_campus_id';
    END IF;
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_users_validate_bu
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_users_validate_bu` BEFORE UPDATE ON `users` FOR EACH ROW BEGIN
  DECLARE v_role_code VARCHAR(30);
  DECLARE v_department_type VARCHAR(20);
  DECLARE v_department_campus_id BIGINT UNSIGNED;

  SELECT role_code INTO v_role_code
  FROM roles
  WHERE role_id = NEW.role_id
    AND status = 'ACTIVE';

  IF v_role_code IS NULL THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Invalid role_id';
  END IF;

  IF v_role_code = 'VISITOR' THEN
    IF NEW.sub_role IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'VISITOR must not have sub_role';
    END IF;
    IF NEW.department_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'VISITOR must not have department_id';
    END IF;
    IF NEW.primary_campus_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'VISITOR must not have primary_campus_id';
    END IF;
  ELSEIF v_role_code IN ('STAFF','DEPARTMENT') THEN
    IF NEW.sub_role IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF/DEPARTMENT must have sub_role';
    END IF;
    IF NEW.department_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF/DEPARTMENT must have department_id';
    END IF;

    SELECT department_type, campus_id
      INTO v_department_type, v_department_campus_id
    FROM departments
    WHERE department_id = NEW.department_id;

    IF v_role_code = 'STAFF' AND v_department_type <> 'IC' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF must belong to IC department';
    END IF;

    IF v_role_code = 'DEPARTMENT' AND v_department_type <> 'GENERAL' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'DEPARTMENT must belong to GENERAL department';
    END IF;

    IF NEW.primary_campus_id IS NULL THEN
      SET NEW.primary_campus_id = v_department_campus_id;
    ELSEIF NEW.primary_campus_id <> v_department_campus_id THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'primary_campus_id must match department campus';
    END IF;
  ELSE
    IF NEW.sub_role IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only STAFF/DEPARTMENT may have sub_role';
    END IF;
    IF NEW.department_id IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only STAFF/DEPARTMENT may have department_id';
    END IF;
    IF NEW.primary_campus_id IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Internal user must have primary_campus_id';
    END IF;
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_visit_campuses_aggregate_ai
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_visit_campuses_aggregate_ai` AFTER INSERT ON `visit_request_campuses` FOR EACH ROW BEGIN
  -- Aggregate theo plan §2.2, BỎ QUA campus đã CANCELLED trong mọi mẫu số.
  -- 'ASSIGNED' ĐƯỢC TÍNH LÀ ĐÃ DUYỆT: approve gán Host và dừng ở ASSIGNED; BEFORE_VISIT chỉ đến
  -- khi Host bấm "Bắt đầu chuẩn bị". Bỏ ASSIGNED khỏi approved_count sẽ làm request tụt về
  -- PENDING_APPROVAL ngay sau khi vừa duyệt xong.
  -- Nhánh đầu tiên là CỔNG XÁC NHẬN: còn campus active thiếu đầu mối -> toàn request đứng ở
  -- PENDING_CONTACT_CONFIRMATION và không Staff Leader nào thấy đơn.
  UPDATE visit_requests vr
  JOIN (
    SELECT visit_request_id,
           SUM(CASE WHEN status <> 'CANCELLED' THEN 1 ELSE 0 END) AS active_count,
           SUM(CASE WHEN status <> 'CANCELLED' AND operational_contact_user_id IS NULL
                    THEN 1 ELSE 0 END) AS unconfirmed_count,
           SUM(CASE WHEN status IN ('ASSIGNED','BEFORE_VISIT','DURING_VISIT','AFTER_VISIT','CLOSED')
                    THEN 1 ELSE 0 END) AS approved_count,
           SUM(CASE WHEN status IN ('WAITING_CONTACT_CONFIRMATION','WAITING_REQUEST_APPROVAL')
                    THEN 1 ELSE 0 END) AS pending_count,
           SUM(CASE WHEN status = 'REJECTED' THEN 1 ELSE 0 END) AS rejected_count
    FROM visit_request_campuses
    WHERE visit_request_id = NEW.visit_request_id
    GROUP BY visit_request_id
  ) s ON s.visit_request_id = vr.visit_request_id
  SET vr.status = CASE
    WHEN vr.status = 'CANCELLED' THEN 'CANCELLED'
    WHEN s.unconfirmed_count > 0 THEN 'PENDING_CONTACT_CONFIRMATION'
    WHEN s.active_count > 0 AND s.rejected_count = s.active_count THEN 'REJECTED'
    WHEN s.approved_count > 0 AND s.pending_count > 0 THEN 'PARTIALLY_APPROVED'
    WHEN s.approved_count > 0 AND s.pending_count = 0 THEN 'APPROVED'
    WHEN s.approved_count = 0 AND s.pending_count > 0 THEN 'PENDING_APPROVAL'
    ELSE vr.status
  END
  WHERE vr.visit_request_id = NEW.visit_request_id
    AND vr.status <> 'CANCELLED';
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_visit_campuses_aggregate_au
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_visit_campuses_aggregate_au` AFTER UPDATE ON `visit_request_campuses` FOR EACH ROW BEGIN
  -- Aggregate theo plan §2.2, BỎ QUA campus đã CANCELLED trong mọi mẫu số.
  -- 'ASSIGNED' ĐƯỢC TÍNH LÀ ĐÃ DUYỆT: approve gán Host và dừng ở ASSIGNED; BEFORE_VISIT chỉ đến
  -- khi Host bấm "Bắt đầu chuẩn bị". Bỏ ASSIGNED khỏi approved_count sẽ làm request tụt về
  -- PENDING_APPROVAL ngay sau khi vừa duyệt xong.
  -- Nhánh đầu tiên là CỔNG XÁC NHẬN: còn campus active thiếu đầu mối -> toàn request đứng ở
  -- PENDING_CONTACT_CONFIRMATION và không Staff Leader nào thấy đơn.
  UPDATE visit_requests vr
  JOIN (
    SELECT visit_request_id,
           SUM(CASE WHEN status <> 'CANCELLED' THEN 1 ELSE 0 END) AS active_count,
           SUM(CASE WHEN status <> 'CANCELLED' AND operational_contact_user_id IS NULL
                    THEN 1 ELSE 0 END) AS unconfirmed_count,
           SUM(CASE WHEN status IN ('ASSIGNED','BEFORE_VISIT','DURING_VISIT','AFTER_VISIT','CLOSED')
                    THEN 1 ELSE 0 END) AS approved_count,
           SUM(CASE WHEN status IN ('WAITING_CONTACT_CONFIRMATION','WAITING_REQUEST_APPROVAL')
                    THEN 1 ELSE 0 END) AS pending_count,
           SUM(CASE WHEN status = 'REJECTED' THEN 1 ELSE 0 END) AS rejected_count
    FROM visit_request_campuses
    WHERE visit_request_id = NEW.visit_request_id
    GROUP BY visit_request_id
  ) s ON s.visit_request_id = vr.visit_request_id
  SET vr.status = CASE
    WHEN vr.status = 'CANCELLED' THEN 'CANCELLED'
    WHEN s.unconfirmed_count > 0 THEN 'PENDING_CONTACT_CONFIRMATION'
    WHEN s.active_count > 0 AND s.rejected_count = s.active_count THEN 'REJECTED'
    WHEN s.approved_count > 0 AND s.pending_count > 0 THEN 'PARTIALLY_APPROVED'
    WHEN s.approved_count > 0 AND s.pending_count = 0 THEN 'APPROVED'
    WHEN s.approved_count = 0 AND s.pending_count > 0 THEN 'PENDING_APPROVAL'
    ELSE vr.status
  END
  WHERE vr.visit_request_id = NEW.visit_request_id
    AND vr.status <> 'CANCELLED';
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_visit_campuses_assignment_validate_bi
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_visit_campuses_assignment_validate_bi` BEFORE INSERT ON `visit_request_campuses` FOR EACH ROW BEGIN
  DECLARE v_request_status VARCHAR(30);
  DECLARE v_registrant_user_id BIGINT UNSIGNED;
  DECLARE v_agenda_count INT DEFAULT 0;
  DECLARE v_host_role_code VARCHAR(30);
  DECLARE v_host_sub_role VARCHAR(30);
  DECLARE v_host_campus_id BIGINT UNSIGNED;
  DECLARE v_assigner_role_code VARCHAR(30);
  DECLARE v_assigner_sub_role VARCHAR(30);
  DECLARE v_assigner_campus_id BIGINT UNSIGNED;
  DECLARE v_decider_role_code VARCHAR(30);
  DECLARE v_decider_sub_role VARCHAR(30);
  DECLARE v_decider_campus_id BIGINT UNSIGNED;
  DECLARE v_decider_status VARCHAR(30);
  DECLARE v_coord_role_code VARCHAR(30);
  DECLARE v_coord_sub_role VARCHAR(30);
  DECLARE v_coord_campus_id BIGINT UNSIGNED;
  DECLARE v_source VARCHAR(40);

  SELECT status, registrant_user_id INTO v_request_status, v_registrant_user_id
  FROM visit_requests
  WHERE visit_request_id = NEW.visit_request_id;

  IF v_request_status = 'CANCELLED' AND NEW.status <> 'CANCELLED' THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Cannot create active campus instance under a cancelled request';
  END IF;

  IF NEW.status IN ('WAITING_CONTACT_CONFIRMATION','WAITING_REQUEST_APPROVAL') THEN
    IF NEW.current_host_user_id IS NOT NULL OR NEW.host_assigned_by IS NOT NULL OR NEW.host_assigned_at IS NOT NULL
       OR NEW.decided_by IS NOT NULL OR NEW.decided_at IS NOT NULL OR NEW.decision_actor_role IS NOT NULL
       OR NEW.decision_source IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Campus instance before a decision must not have host or decision data';
    END IF;
  END IF;

  IF NEW.status = 'REJECTED' THEN
    IF NEW.decided_by IS NULL OR NEW.decided_at IS NULL OR NEW.decision_actor_role <> 'STAFF_LEADER'
       OR NEW.decision_note IS NULL OR TRIM(NEW.decision_note) = '' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'REJECTED campus instance requires Staff Leader decision metadata and decision_note';
    END IF;
    IF NEW.current_host_user_id IS NOT NULL OR NEW.host_assigned_by IS NOT NULL OR NEW.host_assigned_at IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'REJECTED campus instance must not have official host assignment';
    END IF;
  END IF;

  IF NEW.status IN ('ASSIGNED','BEFORE_VISIT','DURING_VISIT','AFTER_VISIT','CLOSED') THEN
    IF NEW.current_host_user_id IS NULL OR NEW.host_assigned_by IS NULL OR NEW.host_assigned_at IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Approved/operational campus instance requires official host assignment';
    END IF;
    IF NEW.decided_by IS NULL OR NEW.decided_at IS NULL OR NEW.decision_actor_role IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Approved/operational campus instance requires decision metadata';
    END IF;
  END IF;

  -- Từ DURING_VISIT trở đi, khách đã/đang được tiếp khách nên campus instance bắt buộc phải có agenda thật.
  -- ASSIGNED và BEFORE_VISIT đều chưa bắt buộc agenda: ASSIGNED là Host chưa mở giai đoạn chuẩn bị,
  -- BEFORE_VISIT là Host đang chuẩn bị agenda.
  IF NEW.status IN ('DURING_VISIT','AFTER_VISIT','CLOSED') THEN
    SELECT COUNT(*) INTO v_agenda_count
    FROM visit_agendas va
    WHERE va.visit_instance_id = NEW.visit_instance_id;

    IF v_agenda_count = 0 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Campus instance cannot be DURING_VISIT/AFTER_VISIT/CLOSED without at least one agenda item';
    END IF;
  END IF;

  IF NEW.coordinator_user_id IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id INTO v_coord_role_code, v_coord_sub_role, v_coord_campus_id
    FROM users u JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.coordinator_user_id;
    IF NOT (v_coord_role_code = 'STAFF' AND v_coord_sub_role = 'LEADER' AND v_coord_campus_id = NEW.campus_id) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'coordinator_user_id must be Staff Leader of the same campus';
    END IF;
  END IF;

  -- Decision rules: the standard Staff Leader review needs a same-campus Staff Leader.
  -- decision_actor_role 'STAFF' exists for exactly ONE case — a regular IC Staff who registered
  -- the request, named THEMSELF as this campus's Host dự kiến, and had that proposal activated
  -- when the confirmation gate opened. It is bound to the proposal columns below, so it cannot be
  -- fabricated: without a matching proposal there is nothing to activate.
  IF NEW.decided_by IS NOT NULL THEN
    SET v_source = COALESCE(NEW.decision_source, 'STANDARD_CAMPUS_REVIEW');

    SELECT r.role_code, u.sub_role, u.primary_campus_id, u.status
      INTO v_decider_role_code, v_decider_sub_role, v_decider_campus_id, v_decider_status
    FROM users u JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.decided_by;

    IF v_source = 'PREAUTHORIZED_HOST_ACTIVATION' THEN
      IF NEW.proposed_host_user_id IS NULL
         OR NEW.proposed_host_by_user_id IS NULL
         OR NOT (NEW.proposed_host_activation_status <=> 'ACTIVATED')
         OR NOT (NEW.current_host_user_id <=> NEW.proposed_host_user_id)
         OR NOT (NEW.decided_by <=> NEW.proposed_host_by_user_id) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PREAUTHORIZED_HOST_ACTIVATION requires an ACTIVATED proposal whose proposed host is the current host and whose proposer is the decider';
      END IF;
    END IF;

    IF NEW.decision_actor_role = 'STAFF' THEN
      IF v_source <> 'PREAUTHORIZED_HOST_ACTIVATION' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'decision_actor_role STAFF is only valid for PREAUTHORIZED_HOST_ACTIVATION';
      END IF;
      IF NOT (v_decider_role_code = 'STAFF' AND v_decider_sub_role = 'STAFF'
              AND v_decider_campus_id = NEW.campus_id AND v_decider_status = 'ACTIVE') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PREAUTHORIZED_HOST_ACTIVATION by STAFF requires an ACTIVE regular Staff of the same campus';
      END IF;
      IF v_registrant_user_id IS NULL OR v_registrant_user_id <> NEW.decided_by THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF self-host decision is only valid on a request registered by that same Staff';
      END IF;
      IF NEW.current_host_user_id IS NULL OR NEW.current_host_user_id <> NEW.decided_by
         OR NEW.host_assigned_by IS NULL OR NEW.host_assigned_by <> NEW.decided_by THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF self-host requires decided_by = host_assigned_by = current_host_user_id';
      END IF;
    ELSE
      IF NEW.decision_actor_role <> 'STAFF_LEADER' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'decision_actor_role must be STAFF_LEADER unless a STAFF proposal is being activated';
      END IF;
      IF NOT (v_decider_role_code = 'STAFF' AND v_decider_sub_role = 'LEADER' AND v_decider_campus_id = NEW.campus_id) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'decided_by must be Staff Leader of the same campus';
      END IF;
    END IF;
  END IF;

  IF NEW.host_assigned_by IS NOT NULL THEN
    IF NEW.decided_by IS NOT NULL AND NEW.decided_by <> NEW.host_assigned_by THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_assigned_by must match decided_by when approving a campus instance';
    END IF;
    -- Same-person STAFF activation case already fully validated above.
    IF NOT (COALESCE(NEW.decision_source,'STANDARD_CAMPUS_REVIEW') = 'PREAUTHORIZED_HOST_ACTIVATION'
            AND NEW.decision_actor_role = 'STAFF') THEN
      SELECT r.role_code, u.sub_role, u.primary_campus_id INTO v_assigner_role_code, v_assigner_sub_role, v_assigner_campus_id
      FROM users u JOIN roles r ON r.role_id = u.role_id
      WHERE u.user_id = NEW.host_assigned_by;
      IF NOT (v_assigner_role_code = 'STAFF' AND v_assigner_sub_role = 'LEADER' AND v_assigner_campus_id = NEW.campus_id) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_assigned_by must be Staff Leader of the same campus';
      END IF;
    END IF;
  END IF;

  IF NEW.current_host_user_id IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id INTO v_host_role_code, v_host_sub_role, v_host_campus_id
    FROM users u JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.current_host_user_id;
    IF NOT (
      (v_host_role_code = 'STAFF' AND v_host_sub_role = 'STAFF' AND v_host_campus_id = NEW.campus_id)
      OR
      (v_host_role_code = 'STAFF' AND v_host_sub_role = 'LEADER' AND v_host_campus_id = NEW.campus_id AND NEW.current_host_user_id = NEW.host_assigned_by)
    ) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'current_host_user_id must be IC Staff of same campus or the approving Staff Leader themself';
    END IF;
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_visit_campuses_assignment_validate_bu
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_visit_campuses_assignment_validate_bu` BEFORE UPDATE ON `visit_request_campuses` FOR EACH ROW BEGIN
  DECLARE v_request_status VARCHAR(30);
  DECLARE v_registrant_user_id BIGINT UNSIGNED;
  DECLARE v_agenda_count INT DEFAULT 0;
  DECLARE v_host_role_code VARCHAR(30);
  DECLARE v_host_sub_role VARCHAR(30);
  DECLARE v_host_campus_id BIGINT UNSIGNED;
  DECLARE v_assigner_role_code VARCHAR(30);
  DECLARE v_assigner_sub_role VARCHAR(30);
  DECLARE v_assigner_campus_id BIGINT UNSIGNED;
  DECLARE v_decider_role_code VARCHAR(30);
  DECLARE v_decider_sub_role VARCHAR(30);
  DECLARE v_decider_campus_id BIGINT UNSIGNED;
  DECLARE v_coord_role_code VARCHAR(30);
  DECLARE v_coord_sub_role VARCHAR(30);
  DECLARE v_coord_campus_id BIGINT UNSIGNED;
  DECLARE v_source VARCHAR(40);
  DECLARE v_is_host_transfer TINYINT DEFAULT 0;

  SELECT status, registrant_user_id INTO v_request_status, v_registrant_user_id
  FROM visit_requests
  WHERE visit_request_id = NEW.visit_request_id;

  IF v_request_status = 'CANCELLED' AND NEW.status <> 'CANCELLED' THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Cannot update campus instance to active status under a cancelled request';
  END IF;

  -- ── Vòng đời campus: mỗi bước phải đến từ đúng bước trước nó ──────────────────────────────
  -- ASSIGNED và BEFORE_VISIT là hai trạng thái KHÁC nhau, không phải hai tên của một trạng thái:
  --   WAITING_REQUEST_APPROVAL -> ASSIGNED      : Staff Leader duyệt + gán Host trong cùng transaction
  --   ASSIGNED                 -> BEFORE_VISIT  : Host bấm "Bắt đầu chuẩn bị" (mở các thao tác setup)
  --   BEFORE_VISIT             -> DURING_VISIT  : Host hoàn tất chuẩn bị
  -- Nếu chỉ dựa vào backend để giữ thứ tự này thì một lệnh UPDATE lạc (script, import, bug) có thể
  -- nhảy cóc và campus vào giai đoạn tiếp khách mà chưa ai chuẩn bị. Guard nằm ở DB nên fail-closed.
  -- Các nhánh REJECTED / CANCELLED / resubmit không bị đụng tới ở đây.
  -- ASSIGNED có HAI đường vào hợp lệ, và cả hai đều nằm sau cổng xác nhận (guard cổng ở
  -- trg_visit_campuses_op_contact_guard_bu):
  --   WAITING_REQUEST_APPROVAL      -> ASSIGNED : Staff Leader duyệt + gán Host
  --   WAITING_CONTACT_CONFIRMATION  -> ASSIGNED : Host dự kiến đã preauthorize được kích hoạt
  --                                               ngay trong transaction đầu mối cuối xác nhận
  IF NEW.status = 'ASSIGNED' AND OLD.status <> 'ASSIGNED'
     AND OLD.status NOT IN ('WAITING_REQUEST_APPROVAL','WAITING_CONTACT_CONFIRMATION') THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Campus instance can only enter ASSIGNED from WAITING_REQUEST_APPROVAL or WAITING_CONTACT_CONFIRMATION';
  END IF;

  -- ... nhưng đường tắt đó CHỈ dành cho kích hoạt proposal. Không có proposal thì cổng mở phải
  -- dẫn tới WAITING_REQUEST_APPROVAL để Staff Leader chọn Host (plan §2.1 nhánh A/E).
  IF NEW.status = 'ASSIGNED' AND OLD.status = 'WAITING_CONTACT_CONFIRMATION'
     AND COALESCE(NEW.decision_source,'') <> 'PREAUTHORIZED_HOST_ACTIVATION' THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'WAITING_CONTACT_CONFIRMATION can only reach ASSIGNED by activating a preauthorized host proposal';
  END IF;

  IF NEW.status = 'BEFORE_VISIT' AND OLD.status <> 'BEFORE_VISIT'
     AND OLD.status <> 'ASSIGNED' THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Campus instance can only enter BEFORE_VISIT from ASSIGNED (Host must start preparation)';
  END IF;

  IF NEW.status = 'DURING_VISIT' AND OLD.status <> 'DURING_VISIT'
     AND OLD.status <> 'BEFORE_VISIT' THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Campus instance can only enter DURING_VISIT from BEFORE_VISIT';
  END IF;

  -- ── Host handover: a deliberate re-assignment on an already-decided, not-yet-started campus. ──
  IF OLD.current_host_user_id IS NOT NULL
     AND NOT (NEW.current_host_user_id <=> OLD.current_host_user_id)
     AND NEW.current_host_user_id IS NOT NULL
     AND OLD.status IN ('ASSIGNED','BEFORE_VISIT')
     AND NEW.status = OLD.status
     AND NEW.host_assigned_by IS NOT NULL
     AND NEW.host_assigned_at IS NOT NULL THEN
    SET v_is_host_transfer = 1;
  END IF;

  IF OLD.current_host_user_id IS NOT NULL
     AND NOT (NEW.current_host_user_id <=> OLD.current_host_user_id)
     AND v_is_host_transfer = 0 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Official host cannot be changed after first assignment';
  END IF;

  -- A decision introduced AFTER creation is either the standard Staff Leader review, or the
  -- activation of a host proposal that was authorized BEFORE the gate opened. The second one is
  -- what makes a regular IC Staff decision legal on an update at all — and it is only legal
  -- because the proposal columns below pin it to a proposal that already existed.
  IF OLD.decided_by IS NULL AND NEW.decided_by IS NOT NULL THEN
    IF COALESCE(NEW.decision_source, 'STANDARD_CAMPUS_REVIEW')
         NOT IN ('STANDARD_CAMPUS_REVIEW','PREAUTHORIZED_HOST_ACTIVATION')
       OR COALESCE(NEW.decision_actor_role,'') NOT IN ('STAFF_LEADER','STAFF') THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Post-create campus decisions must use STANDARD_CAMPUS_REVIEW or PREAUTHORIZED_HOST_ACTIVATION';
    END IF;
    -- The proposal must have been on the row BEFORE this statement. Writing the proposal and
    -- activating it in one UPDATE would let a single write invent its own authorization.
    IF COALESCE(NEW.decision_source,'STANDARD_CAMPUS_REVIEW') = 'PREAUTHORIZED_HOST_ACTIVATION' THEN
      IF OLD.proposed_host_user_id IS NULL
         OR NOT (OLD.proposed_host_user_id <=> NEW.proposed_host_user_id)
         OR NOT (OLD.proposed_host_by_user_id <=> NEW.proposed_host_by_user_id) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PREAUTHORIZED_HOST_ACTIVATION requires a proposal that already existed before this update';
      END IF;
      -- Activation means exactly this: the proposed person becomes the host, and the person who
      -- proposed them is recorded as having decided it. Anything else is a different act wearing
      -- this source's name.
      IF NOT (NEW.proposed_host_activation_status <=> 'ACTIVATED')
         OR NOT (NEW.current_host_user_id <=> NEW.proposed_host_user_id)
         OR NOT (NEW.decided_by <=> NEW.proposed_host_by_user_id) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PREAUTHORIZED_HOST_ACTIVATION requires an ACTIVATED proposal whose proposed host is the current host and whose proposer is the decider';
      END IF;
    END IF;
  END IF;

  IF NEW.status IN ('WAITING_CONTACT_CONFIRMATION','WAITING_REQUEST_APPROVAL') THEN
    IF NEW.current_host_user_id IS NOT NULL OR NEW.host_assigned_by IS NOT NULL OR NEW.host_assigned_at IS NOT NULL
       OR NEW.decided_by IS NOT NULL OR NEW.decided_at IS NOT NULL OR NEW.decision_actor_role IS NOT NULL
       OR NEW.decision_source IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Campus instance before a decision must not have host or decision data';
    END IF;
  END IF;

  IF NEW.status = 'REJECTED' THEN
    IF OLD.status <> 'WAITING_REQUEST_APPROVAL' AND OLD.status <> 'REJECTED' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Only pending campus instance can be rejected';
    END IF;
    IF NEW.decided_by IS NULL OR NEW.decided_at IS NULL OR NEW.decision_actor_role <> 'STAFF_LEADER'
       OR NEW.decision_note IS NULL OR TRIM(NEW.decision_note) = '' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'REJECTED campus instance requires Staff Leader decision metadata and decision_note';
    END IF;
    IF NEW.current_host_user_id IS NOT NULL OR NEW.host_assigned_by IS NOT NULL OR NEW.host_assigned_at IS NOT NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'REJECTED campus instance must not have official host assignment';
    END IF;
  END IF;

  IF NEW.status IN ('ASSIGNED','BEFORE_VISIT','DURING_VISIT','AFTER_VISIT','CLOSED') THEN
    IF NEW.current_host_user_id IS NULL OR NEW.host_assigned_by IS NULL OR NEW.host_assigned_at IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Approved/operational campus instance requires official host assignment';
    END IF;
    IF NEW.decided_by IS NULL OR NEW.decided_at IS NULL OR NEW.decision_actor_role IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Approved/operational campus instance requires decision metadata';
    END IF;
  END IF;

  -- Từ DURING_VISIT trở đi, khách đã/đang được tiếp khách nên campus instance bắt buộc phải có agenda thật.
  -- ASSIGNED và BEFORE_VISIT đều chưa bắt buộc agenda: ASSIGNED là Host chưa mở giai đoạn chuẩn bị,
  -- BEFORE_VISIT là Host đang chuẩn bị agenda.
  IF NEW.status IN ('DURING_VISIT','AFTER_VISIT','CLOSED') THEN
    SELECT COUNT(*) INTO v_agenda_count
    FROM visit_agendas va
    WHERE va.visit_instance_id = NEW.visit_instance_id;

    IF v_agenda_count = 0 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Campus instance cannot be DURING_VISIT/AFTER_VISIT/CLOSED without at least one agenda item';
    END IF;
  END IF;

  IF NEW.coordinator_user_id IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id INTO v_coord_role_code, v_coord_sub_role, v_coord_campus_id
    FROM users u JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.coordinator_user_id;
    IF NOT (v_coord_role_code = 'STAFF' AND v_coord_sub_role = 'LEADER' AND v_coord_campus_id = NEW.campus_id) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'coordinator_user_id must be Staff Leader of the same campus';
    END IF;
  END IF;

  IF NEW.decided_by IS NOT NULL THEN
    SET v_source = COALESCE(NEW.decision_source, 'STANDARD_CAMPUS_REVIEW');

    SELECT r.role_code, u.sub_role, u.primary_campus_id INTO v_decider_role_code, v_decider_sub_role, v_decider_campus_id
    FROM users u JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.decided_by;

    IF v_source = 'PREAUTHORIZED_HOST_ACTIVATION' AND NEW.decision_actor_role = 'STAFF' THEN
      -- Consistency re-check of an activated STAFF self-proposal on every later update.
      IF NOT (v_decider_role_code = 'STAFF' AND v_decider_sub_role = 'STAFF' AND v_decider_campus_id = NEW.campus_id) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PREAUTHORIZED_HOST_ACTIVATION by STAFF requires a regular Staff of the same campus';
      END IF;
      IF v_registrant_user_id IS NULL OR v_registrant_user_id <> NEW.decided_by THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF self-host decision is only valid on a request registered by that same Staff';
      END IF;
      -- On a handover the campus keeps its original self-host DECISION but no longer its original
      -- host, so the decided_by = host trio only has to hold while that decision is being recorded.
      IF v_is_host_transfer = 0
         AND (NEW.current_host_user_id IS NULL OR NEW.current_host_user_id <> NEW.decided_by
              OR NEW.host_assigned_by IS NULL OR NEW.host_assigned_by <> NEW.decided_by) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'STAFF self-host requires decided_by = host_assigned_by = current_host_user_id';
      END IF;
    ELSE
      IF NEW.decision_actor_role <> 'STAFF_LEADER' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'decision_actor_role must be STAFF_LEADER unless an activated STAFF proposal';
      END IF;
      IF NOT (v_decider_role_code = 'STAFF' AND v_decider_sub_role = 'LEADER' AND v_decider_campus_id = NEW.campus_id) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'decided_by must be Staff Leader of the same campus';
      END IF;
    END IF;
  END IF;

  IF NEW.host_assigned_by IS NOT NULL THEN
    -- decided_by and host_assigned_by are the same act only while the decision is being MADE. On a
    -- later handover the assigning leader need not be the one who approved the campus months ago, and
    -- demanding it would force the transfer to overwrite who approved the visit.
    IF OLD.decided_by IS NULL AND NEW.decided_by IS NOT NULL
       AND NEW.decided_by <> NEW.host_assigned_by THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_assigned_by must match decided_by when approving a campus instance';
    END IF;
    IF NOT (COALESCE(NEW.decision_source,'STANDARD_CAMPUS_REVIEW') = 'PREAUTHORIZED_HOST_ACTIVATION'
            AND NEW.decision_actor_role = 'STAFF'
            AND v_is_host_transfer = 0) THEN
      SELECT r.role_code, u.sub_role, u.primary_campus_id INTO v_assigner_role_code, v_assigner_sub_role, v_assigner_campus_id
      FROM users u JOIN roles r ON r.role_id = u.role_id
      WHERE u.user_id = NEW.host_assigned_by;
      IF NOT (v_assigner_role_code = 'STAFF' AND v_assigner_sub_role = 'LEADER' AND v_assigner_campus_id = NEW.campus_id) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'host_assigned_by must be Staff Leader of the same campus';
      END IF;
    END IF;
  END IF;

  -- Host eligibility is UNCHANGED: IC Staff of this campus, or the assigning Staff Leader themself.
  IF NEW.current_host_user_id IS NOT NULL THEN
    SELECT r.role_code, u.sub_role, u.primary_campus_id INTO v_host_role_code, v_host_sub_role, v_host_campus_id
    FROM users u JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.current_host_user_id;
    IF NOT (
      (v_host_role_code = 'STAFF' AND v_host_sub_role = 'STAFF' AND v_host_campus_id = NEW.campus_id)
      OR
      (v_host_role_code = 'STAFF' AND v_host_sub_role = 'LEADER' AND v_host_campus_id = NEW.campus_id AND NEW.current_host_user_id = NEW.host_assigned_by)
    ) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'current_host_user_id must be IC Staff of same campus or the approving Staff Leader themself';
    END IF;
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_visit_campuses_cancel_validate_bu
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_visit_campuses_cancel_validate_bu` BEFORE UPDATE ON `visit_request_campuses` FOR EACH ROW BEGIN
  DECLARE v_request_status VARCHAR(40);
  DECLARE v_registrant_user_id BIGINT UNSIGNED DEFAULT NULL;

  IF NEW.status = 'CANCELLED' AND OLD.status <> 'CANCELLED' THEN
    SELECT status, registrant_user_id INTO v_request_status, v_registrant_user_id
    FROM visit_requests
    WHERE visit_request_id = NEW.visit_request_id;

    -- Campus-level cancel sau APPROVED chỉ áp dụng cho campus chưa bắt đầu.
    -- Campus chưa có quyết định (đang chờ xác nhận đầu mối HOẶC chờ Staff Leader duyệt) chỉ
    -- được chuyển CANCELLED như hệ quả của việc hủy toàn request.
    IF OLD.status IN ('WAITING_CONTACT_CONFIRMATION','WAITING_REQUEST_APPROVAL') THEN
      IF v_request_status <> 'CANCELLED' THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'Pending campus instance can be cancelled only as a consequence of cancelling the pending main request';
      END IF;
    ELSE
      IF v_request_status NOT IN ('APPROVED','PARTIALLY_APPROVED') THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'Campus instance can be cancelled only after at least one campus has been approved, except pending-request cascade cancellation';
      END IF;

      IF OLD.status IN ('DURING_VISIT','AFTER_VISIT','CLOSED') THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'Campus instance already started/finished/closed cannot be cancelled';
      END IF;
    END IF;

    IF NEW.cancelled_by IS NULL OR NEW.cancelled_at IS NULL
       OR NEW.cancellation_actor_type IS NULL OR NEW.cancellation_source IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancelled_by, cancelled_at, cancellation_actor_type and cancellation_source are required when campus instance is cancelled';
    END IF;

    IF NEW.cancellation_reason IS NULL OR TRIM(NEW.cancellation_reason) = '' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancellation_reason is required when campus instance is cancelled';
    END IF;

    -- Visitor tự hủy campus phải trước lịch ít nhất 24 giờ.
    -- Host chỉ được hủy thay khách khi chưa tới giờ bắt đầu tiếp khách; khi đã DURING_VISIT trở đi thì trigger đã chặn theo status.
    IF NEW.cancellation_actor_type = 'VISITOR' AND OLD.planned_start_at < DATE_ADD(NEW.cancelled_at, INTERVAL 24 HOUR) THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Visitor cannot cancel a campus visit within 24 hours of its planned start time';
    END IF;

    IF NEW.cancellation_actor_type = 'HOST' AND NEW.cancelled_at >= OLD.planned_start_at THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'HOST cannot cancel a campus visit after the planned start time';
    END IF;

    IF NEW.cancellation_actor_type = 'VISITOR' THEN
      IF NEW.cancellation_source <> 'SELF_SERVICE' THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'VISITOR campus cancellation must use SELF_SERVICE source';
      END IF;
      -- Actor relation hardening. Guest side of THIS campus = its own operational contact, or the
      -- request's registrant; a sibling campus's contact is not on this campus's guest side.
      -- Rows with neither relation recorded keep the old behaviour rather than blocking.
      IF (v_registrant_user_id IS NOT NULL OR NEW.operational_contact_user_id IS NOT NULL)
         AND NOT (NEW.cancelled_by <=> v_registrant_user_id)
         AND NOT (NEW.cancelled_by <=> NEW.operational_contact_user_id) THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'VISITOR campus cancellation requires cancelled_by to be the registrant or this campus operational contact';
      END IF;
    ELSEIF NEW.cancellation_actor_type = 'HOST' THEN
      IF OLD.status = 'WAITING_REQUEST_APPROVAL' THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'HOST cannot cancel a pending-approval campus instance';
      END IF;
      IF NEW.cancellation_source <> 'EXTERNAL_CONFIRMATION' THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'HOST cancellation on behalf of visitor must use EXTERNAL_CONFIRMATION source';
      END IF;
      IF NEW.current_host_user_id IS NULL OR NEW.cancelled_by <> NEW.current_host_user_id THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'HOST cancellation requires cancelled_by to be the official current host of this campus instance';
      END IF;
    ELSE
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only VISITOR or HOST can cancel a campus instance';
    END IF;
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_visit_campuses_op_contact_guard_bi
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_visit_campuses_op_contact_guard_bi` BEFORE INSERT ON `visit_request_campuses` FOR EACH ROW BEGIN
  DECLARE v_user_count INT DEFAULT 0;
  DECLARE v_user_status VARCHAR(30) DEFAULT NULL;
  DECLARE v_request_status VARCHAR(40) DEFAULT NULL;
  DECLARE v_prop_role VARCHAR(30) DEFAULT NULL;
  DECLARE v_prop_campus BIGINT UNSIGNED DEFAULT NULL;
  DECLARE v_prop_status VARCHAR(30) DEFAULT NULL;

  IF NEW.operational_contact_user_id IS NOT NULL THEN
    SELECT COUNT(*), MAX(u.status)
      INTO v_user_count, v_user_status
    FROM users u
    WHERE u.user_id = NEW.operational_contact_user_id;

    IF v_user_count <> 1 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'OPERATIONAL_CONTACT_USER_NOT_FOUND';
    END IF;

    IF NOT (v_user_status <=> 'ACTIVE') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'OPERATIONAL_CONTACT_ACCOUNT_INACTIVE';
    END IF;
  END IF;

  -- Trạng thái campus và sự có mặt của đầu mối phải khớp nhau.
  IF NEW.status = 'WAITING_CONTACT_CONFIRMATION'
     AND NEW.operational_contact_user_id IS NOT NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'WAITING_CONTACT_CONFIRMATION_MUST_NOT_HAVE_OPERATIONAL_CONTACT';
  END IF;

  IF NEW.status NOT IN ('WAITING_CONTACT_CONFIRMATION','CANCELLED')
     AND NEW.operational_contact_user_id IS NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'CAMPUS_BEYOND_CONFIRMATION_REQUIRES_OPERATIONAL_CONTACT';
  END IF;

  -- ── Host DỰ KIẾN: người được đề xuất phải là nhân sự có thật của ĐÚNG campus này ─────────────
  -- Kiểm ở mức "ai được phép xuất hiện trong ô này", KHÔNG phải eligibility đầy đủ lúc kích hoạt:
  -- revalidation thật (ACTIVE, đúng phòng IC, chưa có host khác, ...) là việc của application tại
  -- thời điểm cổng mở (plan §6.2). Nếu DB đòi eligibility đầy đủ ở mọi thời điểm thì một người
  -- đổi campus sau khi được đề xuất sẽ làm MỌI update lên dòng này thất bại, kể cả hủy campus.
  IF NEW.proposed_host_user_id IS NOT NULL THEN
    SELECT r.role_code, u.primary_campus_id, u.status
      INTO v_prop_role, v_prop_campus, v_prop_status
    FROM users u JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.proposed_host_user_id;

    IF v_prop_role IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PROPOSED_HOST_USER_NOT_FOUND';
    END IF;
    IF NOT (v_prop_role = 'STAFF' AND v_prop_campus <=> NEW.campus_id AND v_prop_status = 'ACTIVE') THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PROPOSED_HOST_MUST_BE_ACTIVE_STAFF_OF_SAME_CAMPUS';
    END IF;
  END IF;

  -- Cổng xác nhận là fail-closed cả ở INSERT. Đường ghi bình thường không bao giờ tạo thẳng một
  -- campus ASSIGNED (proposal được kích hoạt bằng UPDATE sau khi cổng mở), nên nhánh này chỉ bắt
  -- seed sai/sửa tay — đúng chỗ cần bắt nhất.
  IF NEW.status IN ('ASSIGNED','BEFORE_VISIT','DURING_VISIT','AFTER_VISIT','CLOSED','REJECTED') THEN
    SELECT status INTO v_request_status
    FROM visit_requests WHERE visit_request_id = NEW.visit_request_id;

    IF v_request_status <=> 'PENDING_CONTACT_CONFIRMATION' THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CONTACT_CONFIRMATION_REQUIRED';
    END IF;
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_visit_campuses_op_contact_guard_bu
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_visit_campuses_op_contact_guard_bu` BEFORE UPDATE ON `visit_request_campuses` FOR EACH ROW BEGIN
  DECLARE v_user_count INT DEFAULT 0;
  DECLARE v_user_status VARCHAR(30) DEFAULT NULL;
  DECLARE v_request_status VARCHAR(40) DEFAULT NULL;
  DECLARE v_prop_role VARCHAR(30) DEFAULT NULL;
  DECLARE v_prop_campus BIGINT UNSIGNED DEFAULT NULL;
  DECLARE v_prop_status VARCHAR(30) DEFAULT NULL;

  IF NEW.operational_contact_user_id IS NOT NULL
     AND NOT (NEW.operational_contact_user_id <=> OLD.operational_contact_user_id) THEN
    SELECT COUNT(*), MAX(u.status)
      INTO v_user_count, v_user_status
    FROM users u
    WHERE u.user_id = NEW.operational_contact_user_id;

    IF v_user_count <> 1 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'OPERATIONAL_CONTACT_USER_NOT_FOUND';
    END IF;

    IF NOT (v_user_status <=> 'ACTIVE') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'OPERATIONAL_CONTACT_ACCOUNT_INACTIVE';
    END IF;
  END IF;

  IF NEW.status = 'WAITING_CONTACT_CONFIRMATION'
     AND NEW.operational_contact_user_id IS NOT NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'WAITING_CONTACT_CONFIRMATION_MUST_NOT_HAVE_OPERATIONAL_CONTACT';
  END IF;

  IF NEW.status NOT IN ('WAITING_CONTACT_CONFIRMATION','CANCELLED')
     AND NEW.operational_contact_user_id IS NULL THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'CAMPUS_BEYOND_CONFIRMATION_REQUIRES_OPERATIONAL_CONTACT';
  END IF;

  -- ── Host DỰ KIẾN: chỉ kiểm khi đề xuất THAY ĐỔI ──────────────────────────
  -- Kiểm mọi lần update sẽ khóa cứng dòng lại khi người được đề xuất đổi campus/nghỉ việc:
  -- ngay cả lệnh hủy campus cũng không chạy được. Đề xuất cũ không còn hợp lệ là chuyện bình
  -- thường và có đường xử lý riêng (NEEDS_RESELECTION), không phải lỗi ghi.
  IF NEW.proposed_host_user_id IS NOT NULL
     AND NOT (NEW.proposed_host_user_id <=> OLD.proposed_host_user_id) THEN
    SELECT r.role_code, u.primary_campus_id, u.status
      INTO v_prop_role, v_prop_campus, v_prop_status
    FROM users u JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.proposed_host_user_id;

    IF v_prop_role IS NULL THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PROPOSED_HOST_USER_NOT_FOUND';
    END IF;
    IF NOT (v_prop_role = 'STAFF' AND v_prop_campus <=> NEW.campus_id AND v_prop_status = 'ACTIVE') THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'PROPOSED_HOST_MUST_BE_ACTIVE_STAFF_OF_SAME_CAMPUS';
    END IF;
  END IF;

  -- Sau khi campus đã được quyết định, Host chính thức chỉ đổi qua luồng bàn giao Host riêng.
  -- Sửa đề xuất ở đây khi đó là thao tác vô nghĩa và dễ bị nhầm là đã đổi người phụ trách.
  IF OLD.status NOT IN ('WAITING_CONTACT_CONFIRMATION','WAITING_REQUEST_APPROVAL')
     AND (NOT (NEW.proposed_host_user_id <=> OLD.proposed_host_user_id)
          OR NOT (NEW.host_selection_mode <=> OLD.host_selection_mode)) THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'PROPOSED_HOST_CANNOT_CHANGE_AFTER_CAMPUS_DECIDED';
  END IF;

  -- ── Cổng xác nhận toàn cục ────────────────────────────────────────────────
  -- Không campus nào được QUYẾT ĐỊNH (approve/kích hoạt Host dự kiến -> ASSIGNED, hoặc reject)
  -- khi request cha còn đang chờ xác nhận đầu mối. Fail-closed ở tầng DB; backend vẫn phải
  -- validate đầy đủ — trigger không thay thế authorization.
  --
  -- Điều kiện OLD là "chưa được quyết định" chứ KHÔNG phải riêng WAITING_REQUEST_APPROVAL:
  -- kích hoạt Host dự kiến có thể đi thẳng từ WAITING_CONTACT_CONFIRMATION sang ASSIGNED, và
  -- liệt kê thiếu một trạng thái nguồn nào cũng mở lại đúng lỗ hổng này (đã từng xảy ra với
  -- ASSIGNED ở phía NEW).
  IF OLD.status IN ('WAITING_CONTACT_CONFIRMATION','WAITING_REQUEST_APPROVAL')
     AND NEW.status IN ('ASSIGNED','BEFORE_VISIT','REJECTED') THEN
    SELECT status INTO v_request_status
    FROM visit_requests
    WHERE visit_request_id = NEW.visit_request_id;

    IF v_request_status <=> 'PENDING_CONTACT_CONFIRMATION' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'CONTACT_CONFIRMATION_REQUIRED';
    END IF;
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_visit_photos_validate_bi
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='ONLY_FULL_GROUP_BY,STRICT_TRANS_TABLES,NO_ZERO_IN_DATE,NO_ZERO_DATE,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_visit_photos_validate_bi` BEFORE INSERT ON `visit_photos` FOR EACH ROW BEGIN
  DECLARE v_valid_participant INT DEFAULT 0;
  DECLARE v_valid_file INT DEFAULT 0;

  SELECT COUNT(*) INTO v_valid_participant
  FROM visit_request_campuses vrc
  JOIN users u ON u.user_id = NEW.uploaded_by
  LEFT JOIN visit_participants vp ON vp.visit_instance_id = NEW.visit_instance_id
    AND vp.user_id = NEW.uploaded_by
    AND vp.status = 'ACCEPTED'
  WHERE vrc.visit_instance_id = NEW.visit_instance_id
    AND u.status = 'ACTIVE'
    AND (
      vrc.current_host_user_id = NEW.uploaded_by
      OR vp.participant_id IS NOT NULL
    );

  IF v_valid_participant = 0 THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Visit photo uploader must be an ACTIVE user who is Host or an ACCEPTED participant of this visit instance';
  END IF;

  SELECT COUNT(*) INTO v_valid_file
  FROM files f
  WHERE f.file_id = NEW.file_id
    AND f.storage_provider = 'GOOGLE_DRIVE'
    AND f.file_purpose = 'VISIT_REQUEST_PHOTO'
    AND f.mime_type LIKE 'image/%';

  IF v_valid_file = 0 THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Visit photo file must be a Google Drive image with VISIT_REQUEST_PHOTO purpose';
  END IF;

  IF NOT (
    (NEW.status = 'ACTIVE'
      AND NEW.removed_at IS NULL
      AND NEW.removed_by IS NULL
      AND NEW.removal_reason IS NULL)
    OR
    (NEW.status = 'REMOVED'
      AND NEW.removed_at IS NOT NULL
      AND NEW.removed_by IS NOT NULL
      AND NEW.removal_reason IS NOT NULL
      AND TRIM(NEW.removal_reason) <> '')
  ) THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Visit photo removal metadata is inconsistent with status';
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_visit_photos_validate_bu
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_visit_photos_validate_bu` BEFORE UPDATE ON `visit_photos` FOR EACH ROW BEGIN
  IF NEW.visit_request_id <> OLD.visit_request_id
     OR NEW.visit_instance_id <> OLD.visit_instance_id
     OR NEW.visit_photo_folder_id <> OLD.visit_photo_folder_id
     OR NEW.file_id <> OLD.file_id
     OR NEW.uploaded_by <> OLD.uploaded_by
     OR NEW.uploaded_at <> OLD.uploaded_at THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Visit photo ownership, file and original upload metadata are immutable';
  END IF;

  IF NOT (
    (NEW.status = 'ACTIVE'
      AND NEW.removed_at IS NULL
      AND NEW.removed_by IS NULL
      AND NEW.removal_reason IS NULL)
    OR
    (NEW.status = 'REMOVED'
      AND NEW.removed_at IS NOT NULL
      AND NEW.removed_by IS NOT NULL
      AND NEW.removal_reason IS NOT NULL
      AND TRIM(NEW.removal_reason) <> '')
  ) THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT = 'Visit photo removal metadata is inconsistent with status';
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_visit_request_identity_changes_user_guard_bi
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_visit_request_identity_changes_user_guard_bi` BEFORE INSERT ON `visit_request_identity_changes` FOR EACH ROW BEGIN
  DECLARE v_user_count INT DEFAULT 0;
  DECLARE v_user_status VARCHAR(30) DEFAULT NULL;

  IF NEW.new_user_id IS NOT NULL THEN
    SELECT COUNT(*), MAX(u.status)
      INTO v_user_count, v_user_status
    FROM users u
    WHERE u.user_id = NEW.new_user_id;

    IF v_user_count <> 1 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'OPERATIONAL_CONTACT_USER_NOT_FOUND';
    END IF;

    IF NOT (v_user_status <=> 'ACTIVE') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'OPERATIONAL_CONTACT_ACCOUNT_INACTIVE';
    END IF;
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_visit_request_identity_changes_user_guard_bu
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_visit_request_identity_changes_user_guard_bu` BEFORE UPDATE ON `visit_request_identity_changes` FOR EACH ROW BEGIN
  DECLARE v_user_count INT DEFAULT 0;
  DECLARE v_user_status VARCHAR(30) DEFAULT NULL;

  IF NEW.new_user_id IS NOT NULL THEN
    SELECT COUNT(*), MAX(u.status)
      INTO v_user_count, v_user_status
    FROM users u
    WHERE u.user_id = NEW.new_user_id;

    IF v_user_count <> 1 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'OPERATIONAL_CONTACT_USER_NOT_FOUND';
    END IF;

    IF NOT (v_user_status <=> 'ACTIVE') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'OPERATIONAL_CONTACT_ACCOUNT_INACTIVE';
    END IF;
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_visit_requests_cancel_validate_bu
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_visit_requests_cancel_validate_bu` BEFORE UPDATE ON `visit_requests` FOR EACH ROW BEGIN
  DECLARE v_cancel_role_code VARCHAR(30);
  DECLARE v_started_campus_count INT DEFAULT 0;
  DECLARE v_cancel_window_violation_count INT DEFAULT 0;

  IF NEW.status = 'CANCELLED' AND OLD.status <> 'CANCELLED' THEN
    -- Người đăng ký hủy được khi đơn còn đang chờ xác nhận đầu mối, còn chờ duyệt, hoặc đã
    -- duyệt nhưng chưa campus nào bắt đầu.
    IF OLD.status NOT IN ('APPROVED', 'PARTIALLY_APPROVED', 'PENDING_APPROVAL', 'PENDING_CONTACT_CONFIRMATION') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only pending or approved request/delegation can be cancelled';
    END IF;

    IF NEW.cancelled_by IS NULL OR NEW.cancelled_at IS NULL THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancelled_by and cancelled_at are required when request is cancelled';
    END IF;

    IF NEW.cancellation_reason IS NULL OR TRIM(NEW.cancellation_reason) = '' THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'cancellation_reason is required when request/delegation is cancelled';
    END IF;

    SELECT r.role_code INTO v_cancel_role_code
    FROM users u
    JOIN roles r ON r.role_id = u.role_id
    WHERE u.user_id = NEW.cancelled_by;

    -- Hủy TOÀN REQUEST là quyền của NGƯỜI ĐĂNG KÝ, và chỉ người đó. Không còn đầu mối
    -- cấp request để tranh chấp quyền này; đầu mối vận hành chỉ thao tác trên đúng campus
    -- của mình. NULL-safe throughout.
    IF NEW.registrant_user_id IS NULL OR NEW.cancelled_by <> NEW.registrant_user_id THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Only the registrant can cancel the whole request';
    END IF;

    -- Người đăng ký có thể là VISITOR, STAFF hoặc STAFF LEADER (role tạo đơn hợp lệ).
    -- HO/ADMIN/DEPARTMENT/STUDENT không bao giờ có quyền này qua role.
    IF v_cancel_role_code NOT IN ('VISITOR','STAFF') THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Request cancel is limited to VISITOR/STAFF create-group accounts';
    END IF;

    -- Visitor self-service cancellation must be at least 24 hours before every active campus schedule.
    -- Backend should still enforce the same rule for better messages/timezone handling; this trigger is DB safety.
    SELECT COUNT(*) INTO v_cancel_window_violation_count
    FROM visit_request_campuses vrc
    WHERE vrc.visit_request_id = OLD.visit_request_id
      AND vrc.status NOT IN ('CANCELLED','REJECTED')
      AND vrc.planned_start_at < DATE_ADD(NEW.cancelled_at, INTERVAL 24 HOUR);

    IF v_cancel_window_violation_count > 0 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Visitor cannot cancel the main visit request within 24 hours of any active campus visit';
    END IF;

    -- Sau khi request đã APPROVED, không cho hủy tổng nếu bất kỳ campus nào đã bắt đầu/đã diễn ra/đã đóng.
    -- Khi đó Visitor phải hủy từng campus còn chưa bắt đầu ở visit_request_campuses level.
    IF OLD.status IN ('APPROVED','PARTIALLY_APPROVED') THEN
      SELECT COUNT(*) INTO v_started_campus_count
      FROM visit_request_campuses vrc
      WHERE vrc.visit_request_id = OLD.visit_request_id
        AND vrc.status IN ('DURING_VISIT','AFTER_VISIT','CLOSED');

      IF v_started_campus_count > 0 THEN
        SIGNAL SQLSTATE '45000'
          SET MESSAGE_TEXT = 'Request has campus visit(s) already started; cancel each not-yet-started campus instead of cancelling the whole request';
      END IF;
    END IF;
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

-- Dumping structure for trigger pems_db.trg_visit_requests_contact_gate_guard_bu
SET @OLDTMP_SQL_MODE=@@SQL_MODE, SQL_MODE='STRICT_TRANS_TABLES,ERROR_FOR_DIVISION_BY_ZERO,NO_ENGINE_SUBSTITUTION';
DELIMITER //
CREATE TRIGGER `trg_visit_requests_contact_gate_guard_bu` BEFORE UPDATE ON `visit_requests` FOR EACH ROW BEGIN
  DECLARE v_unconfirmed_count INT DEFAULT 0;

  IF OLD.status = 'PENDING_CONTACT_CONFIRMATION'
     AND NEW.status NOT IN ('PENDING_CONTACT_CONFIRMATION','CANCELLED') THEN
    SELECT COUNT(*) INTO v_unconfirmed_count
    FROM visit_request_campuses vrc
    WHERE vrc.visit_request_id = NEW.visit_request_id
      AND vrc.status <> 'CANCELLED'
      AND vrc.operational_contact_user_id IS NULL;

    IF v_unconfirmed_count > 0 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'CONTACT_CONFIRMATION_REQUIRED';
    END IF;
  END IF;
END//
DELIMITER ;
SET SQL_MODE=@OLDTMP_SQL_MODE;

/*!40103 SET TIME_ZONE=IFNULL(@OLD_TIME_ZONE, 'system') */;
/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IFNULL(@OLD_FOREIGN_KEY_CHECKS, 1) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40111 SET SQL_NOTES=IFNULL(@OLD_SQL_NOTES, 1) */;
