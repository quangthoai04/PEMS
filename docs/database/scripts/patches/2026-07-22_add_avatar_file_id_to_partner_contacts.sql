-- Patch to add avatar_file_id to partner_contacts
ALTER TABLE `partner_contacts` ADD COLUMN `avatar_file_id` BIGINT UNSIGNED NULL AFTER `scanned_card_file_id`;
