-- Make phone numbers optional for visit requests
ALTER TABLE visit_requests
    MODIFY COLUMN registrant_phone VARCHAR(50) NULL,
    MODIFY COLUMN contact_person_phone VARCHAR(50) NULL;

ALTER TABLE visit_instance_form_details
    MODIFY COLUMN operational_contact_phone VARCHAR(50) NULL;
