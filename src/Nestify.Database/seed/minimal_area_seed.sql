-- Minimal area seed data, matching MockAreaService's test list exactly.
-- Purpose: unblock testing of helper registration (and any other feature
-- that references divisions/districts/upazilas) before the full national
-- dataset is seeded.
--
-- Safe to run multiple times: uses ON CONFLICT DO NOTHING.

INSERT INTO divisions (id, name, bn_name) VALUES
    (1, 'Dhaka',      'ঢাকা'),
    (2, 'Chattogram', 'চট্টগ্রাম'),
    (3, 'Sylhet',     'সিলেট')
ON CONFLICT (id) DO NOTHING;

INSERT INTO districts (id, division_id, name, bn_name) VALUES
    (101, 1, 'Dhaka',        'ঢাকা'),
    (102, 1, 'Gazipur',      'গাজীপুর'),
    (103, 1, 'Narayanganj',  'নারায়ণগঞ্জ'),
    (201, 2, 'Chattogram',   'চট্টগ্রাম'),
    (202, 2, 'Cox''s Bazar', 'কক্সবাজার'),
    (301, 3, 'Sylhet',       'সিলেট'),
    (302, 3, 'Moulvibazar',  'মৌলভীবাজার')
ON CONFLICT (id) DO NOTHING;

INSERT INTO upazilas (id, district_id, name, bn_name, is_metropolitan_thana) VALUES
    (10101, 101, 'Dhanmondi',        'ধানমন্ডি',        true),
    (10102, 101, 'Mirpur',           'মিরপুর',           true),
    (10103, 101, 'Mohammadpur',      'মোহাম্মদপুর',      true),
    (10201, 102, 'Tongi',            'টঙ্গী',            false),
    (10202, 102, 'Sreepur',          'শ্রীপুর',          false),
    (10301, 103, 'Siddhirganj',      'সিদ্ধিরগঞ্জ',      false),
    (20101, 201, 'Panchlaish',       'পাঁচলাইশ',         true),
    (20102, 201, 'Kotwali',          'কোতোয়ালী',        true),
    (20201, 202, 'Cox''s Bazar Sadar','কক্সবাজার সদর',   false),
    (30101, 301, 'Sylhet Sadar',     'সিলেট সদর',        true),
    (30201, 302, 'Sreemangal',       'শ্রীমঙ্গল',        false)
ON CONFLICT (id) DO NOTHING;
SELECT COUNT(*) FROM upazilas;
SELECT * FROM domestic_helper_profiles;
