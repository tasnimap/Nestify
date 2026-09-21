-- Sample domestic helpers so the Domestic Help page has something to show.
-- Needs Autthintication.sql, Bangladesh_Administrative_Structure.sql (with its
-- seed) and Domestic_Help.sql applied first.
--
-- Six helper accounts around Dhaka, all with password  Helper@123
-- (hashed with pgcrypto's bcrypt, which BCrypt.Net verifies). No engagements
-- or reviews are seeded: a review can only come from a bachelor who really
-- took the service. Safe to run again, an existing email is skipped.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

DROP TABLE IF EXISTS seed_helpers;
CREATE TEMP TABLE seed_helpers (
    email       text,
    full_name   text,
    phone       text,
    headline    text,
    bio         text,
    languages   text,
    years       int,
    rate        numeric,
    upazila_id  int,
    address     text,
    lat         numeric,
    lng         numeric,
    services    smallint[]
);

INSERT INTO seed_helpers VALUES
    ('rahima.begum@nestify.demo', 'Rahima Begum', '01711000101',
     'Cooking and cleaning for bachelor flats, 7 years',
     'I have cooked for shared flats in Dhanmondi since 2019. Bangla food, simple continental, and I keep the kitchen tidy after cooking. Used to four or five people with different meal times.',
     'Bangla', 7, 9000, 90012, 'Road 8/A, Dhanmondi', 23.745600, 90.374800, '{1,2,4}'),
    ('salma.akter@nestify.demo', 'Salma Akter', '01811000102',
     'Deep cleaning and laundry, twice a week homes welcome',
     'Cleaning is what I do best: kitchens, bathrooms, floors and balconies. Laundry folded and sorted by room. I bring my own cleaning kit if you do not have one.',
     'Bangla', 5, 6500, 90027, 'Block C, Tajmahal Road, Mohammadpur', 23.765100, 90.358900, '{2,3}'),
    ('momena.khatun@nestify.demo', 'Momena Khatun', '01911000103',
     'Lunch and dinner for messes, bazar included',
     'I cook for a mess of eight in Kalabagan and one flat of three. Weekly bazar with receipts, no wastage. Khichuri, bhuna, dal, vegetables and fish the way home tastes.',
     'Bangla, basic English', 10, 11000, 90020, 'Lake Circus, Kalabagan', 23.748900, 90.383200, '{1,5}'),
    ('shirin.sultana@nestify.demo', 'Shirin Sultana', '01611000104',
     'All-round help for a shared flat',
     'Cooking, cleaning, dishes and the odd grocery run, whatever the flat needs that day. Punctual and quiet; I have worked with students who study late and sleep late.',
     'Bangla', 4, 8000, 90026, 'Section 10, Mirpur', 23.807100, 90.368400, '{1,2,4,6}'),
    ('fatema.bibi@nestify.demo', 'Fatema Bibi', '01511000105',
     'Morning cook for working bachelors',
     'Breakfast and lunch boxes ready before you leave for the office. I work mornings only, 6 to 11, in Gulshan and Banani.',
     'Bangla, English', 6, 7500, 90014, 'Road 27, Gulshan 1', 23.780600, 90.416700, '{1}'),
    ('nasrin.parvin@nestify.demo', 'Nasrin Parvin', '01311000106',
     'Cleaning and dishwashing, evenings',
     'Evening rounds after everyone is back: dishes, kitchen, bathrooms and a sweep of the rooms. Three flats in Badda already, room for one more.',
     'Bangla', 3, 5500, 90003, 'Merul Badda', 23.774300, 90.425900, '{2,4}');

-- users + roles
INSERT INTO users (full_name, email, password_hash, phone_number, account_type)
SELECT s.full_name, s.email, crypt('Helper@123', gen_salt('bf', 11)), s.phone, 2
FROM seed_helpers s
WHERE NOT EXISTS (SELECT 1 FROM users u WHERE u.email = s.email);

INSERT INTO user_roles (user_id, role_id)
SELECT u.id, 2
FROM users u JOIN seed_helpers s ON s.email = u.email
ON CONFLICT DO NOTHING;

-- profiles
INSERT INTO domestic_helper_profiles (user_id, headline, bio, languages, years_experience, monthly_rate, is_active)
SELECT u.id, s.headline, s.bio, s.languages, s.years, s.rate, true
FROM users u JOIN seed_helpers s ON s.email = u.email
WHERE NOT EXISTS (SELECT 1 FROM domestic_helper_profiles hp WHERE hp.user_id = u.id);

-- where they live
INSERT INTO helper_addresses (helper_profile_id, upazila_id, address_line, latitude, longitude)
SELECT hp.id, s.upazila_id, s.address, s.lat, s.lng
FROM domestic_helper_profiles hp
JOIN users u ON u.id = hp.user_id
JOIN seed_helpers s ON s.email = u.email
ON CONFLICT (helper_profile_id) DO NOTHING;

-- services
INSERT INTO helper_services (helper_profile_id, service_type)
SELECT hp.id, unnest(s.services)
FROM domestic_helper_profiles hp
JOIN users u ON u.id = hp.user_id
JOIN seed_helpers s ON s.email = u.email
ON CONFLICT DO NOTHING;

-- weekly board: every day but Friday, 8 AM to 6 PM; Fatema mornings only,
-- Nasrin evenings only
INSERT INTO helper_weekly_availability (helper_profile_id, day_of_week, hour)
SELECT hp.id, d.day, h.hour
FROM domestic_helper_profiles hp
JOIN users u ON u.id = hp.user_id
JOIN seed_helpers s ON s.email = u.email
CROSS JOIN generate_series(0, 6) AS d(day)
CROSS JOIN generate_series(6, 23) AS h(hour)
WHERE d.day <> 5
  AND h.hour >= CASE WHEN s.email = 'nasrin.parvin@nestify.demo' THEN 16 ELSE 8 END
  AND h.hour <  CASE WHEN s.email = 'fatema.bibi@nestify.demo' THEN 11 WHEN s.email = 'nasrin.parvin@nestify.demo' THEN 22 ELSE 18 END
ON CONFLICT DO NOTHING;

DROP TABLE seed_helpers;
