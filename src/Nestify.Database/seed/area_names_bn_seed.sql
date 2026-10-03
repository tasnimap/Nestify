-- ============================================================================
-- Seed data for area_names_bn (Bangla names for the administrative areas)
--
-- Run AFTER bangladesh_administrative_seed.sql.
-- Safe to run more than once; existing rows are updated in place.
--
--   1. Divisions, districts and rural upazilas copy the Bangla spelling that
--      the administrative seed already carries in bn_name.
--   2. The 108 metropolitan thanas (ids 90001+) had no Bangla name, so they
--      are listed here.
-- ============================================================================

BEGIN;

INSERT INTO area_names_bn (area_type, area_id, name_bn)
SELECT 'division', id, bn_name FROM divisions WHERE bn_name IS NOT NULL
ON CONFLICT (area_type, area_id) DO UPDATE SET name_bn = EXCLUDED.name_bn;

INSERT INTO area_names_bn (area_type, area_id, name_bn)
SELECT 'district', id, bn_name FROM districts WHERE bn_name IS NOT NULL
ON CONFLICT (area_type, area_id) DO UPDATE SET name_bn = EXCLUDED.name_bn;

INSERT INTO area_names_bn (area_type, area_id, name_bn)
SELECT 'upazila', id, bn_name FROM upazilas WHERE bn_name IS NOT NULL
ON CONFLICT (area_type, area_id) DO UPDATE SET name_bn = EXCLUDED.name_bn;

-- Metropolitan thanas
INSERT INTO area_names_bn (area_type, area_id, name_bn) VALUES
    -- Dhaka
    ('upazila', 90001, 'আদাবর'),
    ('upazila', 90002, 'বিমানবন্দর'),
    ('upazila', 90003, 'বাড্ডা'),
    ('upazila', 90004, 'বনানী'),
    ('upazila', 90005, 'বংশাল'),
    ('upazila', 90006, 'ভাষানটেক'),
    ('upazila', 90007, 'ক্যান্টনমেন্ট'),
    ('upazila', 90008, 'চকবাজার'),
    ('upazila', 90009, 'দক্ষিণখান'),
    ('upazila', 90010, 'দারুস সালাম'),
    ('upazila', 90011, 'ডেমরা'),
    ('upazila', 90012, 'ধানমন্ডি'),
    ('upazila', 90013, 'গেন্ডারিয়া'),
    ('upazila', 90014, 'গুলশান'),
    ('upazila', 90015, 'হাতিরঝিল'),
    ('upazila', 90016, 'হাজারীবাগ'),
    ('upazila', 90017, 'যাত্রাবাড়ী'),
    ('upazila', 90018, 'কদমতলী'),
    ('upazila', 90019, 'কাফরুল'),
    ('upazila', 90020, 'কলাবাগান'),
    ('upazila', 90021, 'কামরাঙ্গীরচর'),
    ('upazila', 90022, 'খিলগাঁও'),
    ('upazila', 90023, 'খিলক্ষেত'),
    ('upazila', 90024, 'কোতোয়ালী'),
    ('upazila', 90025, 'লালবাগ'),
    ('upazila', 90026, 'মিরপুর মডেল'),
    ('upazila', 90027, 'মোহাম্মদপুর'),
    ('upazila', 90028, 'মতিঝিল'),
    ('upazila', 90029, 'মুগদা'),
    ('upazila', 90030, 'নিউ মার্কেট'),
    ('upazila', 90031, 'পল্লবী'),
    ('upazila', 90032, 'পল্টন মডেল'),
    ('upazila', 90033, 'রমনা মডেল'),
    ('upazila', 90034, 'রামপুরা'),
    ('upazila', 90035, 'রূপনগর'),
    ('upazila', 90036, 'সবুজবাগ'),
    ('upazila', 90037, 'শাহ আলী'),
    ('upazila', 90038, 'শাহবাগ'),
    ('upazila', 90039, 'শাহজাহানপুর'),
    ('upazila', 90040, 'শেরেবাংলা নগর'),
    ('upazila', 90041, 'শ্যামপুর'),
    ('upazila', 90042, 'সূত্রাপুর'),
    ('upazila', 90043, 'তেজগাঁও'),
    ('upazila', 90044, 'তেজগাঁও শিল্পাঞ্চল'),
    ('upazila', 90045, 'তুরাগ'),
    ('upazila', 90046, 'উত্তরখান'),
    ('upazila', 90047, 'উত্তরা পূর্ব'),
    ('upazila', 90048, 'উত্তরা পশ্চিম'),
    ('upazila', 90049, 'ভাটারা'),
    ('upazila', 90050, 'ওয়ারী'),
    -- Chattogram
    ('upazila', 90051, 'আকবরশাহ'),
    ('upazila', 90052, 'বাকলিয়া'),
    ('upazila', 90053, 'বন্দর'),
    ('upazila', 90054, 'বায়েজিদ'),
    ('upazila', 90055, 'চান্দগাঁও'),
    ('upazila', 90056, 'ডবলমুরিং'),
    ('upazila', 90057, 'হালিশহর'),
    ('upazila', 90058, 'খুলশী'),
    ('upazila', 90059, 'কোতোয়ালী'),
    ('upazila', 90060, 'পাহাড়তলী'),
    ('upazila', 90061, 'পাঁচলাইশ'),
    ('upazila', 90062, 'পতেঙ্গা'),
    ('upazila', 90063, 'চকবাজার'),
    ('upazila', 90064, 'সদরঘাট'),
    ('upazila', 90065, 'ইপিজেড'),
    ('upazila', 90066, 'কর্ণফুলী'),
    -- Khulna
    ('upazila', 90067, 'খুলনা সদর'),
    ('upazila', 90068, 'সোনাডাঙ্গা'),
    ('upazila', 90069, 'লবণচরা'),
    ('upazila', 90070, 'হরিণটানা'),
    ('upazila', 90071, 'খালিশপুর'),
    ('upazila', 90072, 'দৌলতপুর'),
    ('upazila', 90073, 'খান জাহান আলী'),
    ('upazila', 90074, 'আড়ংঘাটা'),
    -- Rajshahi
    ('upazila', 90075, 'বোয়ালিয়া'),
    ('upazila', 90076, 'রাজপাড়া'),
    ('upazila', 90077, 'মতিহার'),
    ('upazila', 90078, 'শাহ মখদুম'),
    ('upazila', 90079, 'চন্দ্রিমা'),
    ('upazila', 90080, 'কাশিয়াডাঙ্গা'),
    ('upazila', 90081, 'কাটাখালী'),
    ('upazila', 90082, 'বেলপুকুর'),
    ('upazila', 90083, 'বিমানবন্দর'),
    ('upazila', 90084, 'কর্ণহার'),
    ('upazila', 90085, 'দামকুড়া'),
    -- Sylhet
    ('upazila', 90086, 'কোতোয়ালী মডেল'),
    ('upazila', 90087, 'দক্ষিণ সুরমা'),
    ('upazila', 90088, 'মোগলাবাজার'),
    ('upazila', 90089, 'জালালাবাদ'),
    ('upazila', 90090, 'বিমানবন্দর'),
    ('upazila', 90091, 'শাহ পরান'),
    -- Barishal
    ('upazila', 90092, 'কোতোয়ালী মডেল'),
    ('upazila', 90093, 'বিমানবন্দর'),
    ('upazila', 90094, 'কাউনিয়া'),
    ('upazila', 90095, 'বন্দর'),
    -- Rangpur
    ('upazila', 90096, 'কোতোয়ালী'),
    ('upazila', 90097, 'পরশুরাম'),
    ('upazila', 90098, 'হারাগাছ'),
    ('upazila', 90099, 'তাজহাট'),
    ('upazila', 90100, 'মাহিগঞ্জ'),
    ('upazila', 90101, 'হাজিরহাট'),
    -- Gazipur
    ('upazila', 90102, 'বাসন'),
    ('upazila', 90103, 'গাছা'),
    ('upazila', 90104, 'জয়দেবপুর'),
    ('upazila', 90105, 'কাশিমপুর'),
    ('upazila', 90106, 'পূবাইল'),
    ('upazila', 90107, 'টঙ্গী পূর্ব'),
    ('upazila', 90108, 'টঙ্গী পশ্চিম')
ON CONFLICT (area_type, area_id) DO UPDATE SET name_bn = EXCLUDED.name_bn;

COMMIT;
