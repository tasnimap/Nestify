"""Builds bangladesh_administrative_seed.sql.

Run from this folder:  python build_admin_seed.py

Divisions, districts and rural upazilas are downloaded from nuhil/bangladesh-geocode
(MIT licensed), which carries English and Bangla names for all three levels plus
district coordinates.

That dataset only covers rural upazilas -- its Dhaka district holds just Savar,
Dhamrai, Keraniganj, Nawabganj and Dohar, with no Dhanmondi, Gulshan or Mirpur.
The metropolitan thanas below therefore come from the English Wikipedia article of
each of the eight metropolitan police forces (checked September 2026); the article
is named in the comment above each list. They are kept here as literals rather than
scraped, because the eight articles write their lists in four different formats
(bulleted list, numbered list, wikitable, anchored prose) and a scraper over them
breaks on the next edit.
"""

import json
import urllib.request

GEOCODE = "https://raw.githubusercontent.com/nuhil/bangladesh-geocode/master"

OUTPUT = "bangladesh_administrative_seed.sql"

# Ids for metropolitan thanas start here, above anything the source dataset uses.
METRO_ID_START = 90001

# The source dataset still uses some pre-2018 spellings. These are the official
# ones, and they are what the rest of the application shows.
NAME_FIXES = {
    "Chattagram": "Chattogram",
    "Chittagong": "Chattogram",
    "Barisal": "Barishal",
    "Comilla": "Cumilla",
    "Bogra": "Bogura",
    "Jessore": "Jashore",
    "Coxsbazar": "Cox's Bazar",
    "Coxsbazar Sadar": "Cox's Bazar Sadar",
    "Netrokona": "Netrakona",
}

# Each entry: district name (official spelling, as written out below) -> thanas.
METROPOLITAN_THANAS = {
    # Dhaka Metropolitan Police (en.wikipedia.org/wiki/Dhaka_Metropolitan_Police)
    "Dhaka": [
        "Adabor", "Airport", "Badda", "Banani", "Bangshal", "Bhashantek",
        "Cantonment", "Chackbazar", "Dakshin Khan", "Darus-Salam", "Demra",
        "Dhanmondi", "Gandaria", "Gulshan", "Hatirjheel", "Hazaribagh",
        "Jattrabari", "Kadamtoli", "Kafrul", "Kalabagan", "Kamrangirchar",
        "Khilgaon", "Khilkhet", "Kotwali", "Lalbagh", "Mirpur Model",
        "Mohammadpur", "Motijheel", "Mugda", "New Market", "Pallabi",
        "Paltan Model", "Ramna Model", "Rampura", "Rupnagar", "Sabujbag",
        "Shah Ali", "Shahbag", "Shahjahanpur", "Sher e Bangla Nagar", "Shyampur",
        "Sutrapur", "Tejgaon", "Tejgaon Industrial", "Turag", "Uttar Khan",
        "Uttara East", "Uttara West", "Vatara", "Wari",
    ],
    # Chattogram Metropolitan Police (en.wikipedia.org/wiki/Chittagong_Metropolitan_Police)
    "Chattogram": [
        "Akbarshah", "Bakoliya", "Bandar", "Bayazid", "Chandgaon",
        "Double Mooring", "Halishahar", "Khulshi", "Kotwali", "Pahartali",
        "Panchlaish", "Patenga", "Chawkbazar", "Sadarghat", "EPZ", "Karnaphuli",
    ],
    # Khulna Metropolitan Police (en.wikipedia.org/wiki/Khulna_Metropolitan_Police)
    "Khulna": [
        "Khulna Sadar", "Sonadanga", "Labanchara", "Harintana", "Khalishpur",
        "Daulatpur", "Khan Jahan Ali", "Aranghata",
    ],
    # Rajshahi Metropolitan Police (en.wikipedia.org/wiki/Rajshahi_Metropolitan_Police)
    "Rajshahi": [
        "Boalia", "Rajpara", "Motihar", "Shah Makhdum", "Chandrima",
        "Kasiadanga", "Katakhali", "Belpukur", "Airport", "Karnahar",
        "Damkura", "Paba",
    ],
    # Sylhet Metropolitan Police (en.wikipedia.org/wiki/Sylhet_Metropolitan_Police)
    "Sylhet": [
        "Kotwali Model", "South Surma", "Moglabazar", "Jalalabad",
        "Bimanbandar", "Shah Poran",
    ],
    # Barishal Metropolitan Police (en.wikipedia.org/wiki/Barisal_Metropolitan_Police)
    # The four thanas in service; the article lists four more as proposed only.
    "Barishal": [
        "Kotwali Model", "Airport", "Kawnia", "Bandar",
    ],
    # Rangpur Metropolitan Police (en.wikipedia.org/wiki/Rangpur_Metropolitan_Police)
    "Rangpur": [
        "Kotwali", "Parshuram", "Haragach", "Tajhat", "Mahiganj", "Hazirhat",
    ],
    # Gazipur Metropolitan Police (en.wikipedia.org/wiki/Gazipur_Metropolitan_Police)
    "Gazipur": [
        "Bason", "Gacha", "Joydebpur", "Kashimpur", "Pubail", "Tongi East",
        "Tongi West",
    ],
}


def fetch(path):
    """Reads one of the phpMyAdmin style JSON exports from the geocode repo."""
    url = f"{GEOCODE}/{path}"
    request = urllib.request.Request(url, headers={"User-Agent": "nestify-seed/1.0"})
    with urllib.request.urlopen(request, timeout=90) as response:
        payload = json.load(response)

    if isinstance(payload, list) and isinstance(payload[0], dict) and payload[0].get("type") == "header":
        for block in payload:
            if block.get("type") == "table":
                return block["data"]
    return payload


def official(name):
    return NAME_FIXES.get(name.strip(), name.strip())


def quote(value):
    if value is None or value == "":
        return "NULL"
    return "'" + str(value).strip().replace("'", "''") + "'"


def number(value):
    if value is None or str(value).strip() == "":
        return "NULL"
    try:
        return f"{float(value):.6f}"
    except ValueError:
        return "NULL"


def main():
    divisions = fetch("divisions/divisions.json")
    districts = fetch("districts/districts.json")
    upazilas = fetch("upazilas/upazilas.json")

    lines = []
    add = lines.append

    add("-- ============================================================================")
    add("-- Seed data for Bangladesh_Administrative_Structure.sql")
    add("--")
    add("-- Generated by build_admin_seed.py. Do not edit by hand; edit the script and")
    add("-- run it again.")
    add("--")
    add("-- Divisions, districts and rural upazilas: nuhil/bangladesh-geocode (MIT).")
    add("-- Metropolitan thanas: English Wikipedia articles of the eight metropolitan")
    add("-- police forces, listed in the script.")
    add("--")
    add("-- Safe to run more than once; every insert skips rows that are already there.")
    add("-- ============================================================================")
    add("")
    add("BEGIN;")
    add("")
    add(f"-- {len(divisions)} divisions")
    add("INSERT INTO divisions (id, name, bn_name) VALUES")

    rows = [f"    ({d['id']}, {quote(official(d['name']))}, {quote(d['bn_name'])})" for d in divisions]
    add(",\n".join(rows))
    add("ON CONFLICT (id) DO NOTHING;")
    add("")

    add(f"-- {len(districts)} districts")
    add("INSERT INTO districts (id, division_id, name, bn_name, latitude, longitude) VALUES")
    rows = [
        f"    ({d['id']}, {d['division_id']}, {quote(official(d['name']))}, {quote(d['bn_name'])}, "
        f"{number(d.get('lat'))}, {number(d.get('lon'))})"
        for d in districts
    ]
    add(",\n".join(rows))
    add("ON CONFLICT (id) DO NOTHING;")
    add("")

    add(f"-- {len(upazilas)} upazilas")
    add("INSERT INTO upazilas (id, district_id, name, bn_name, is_metropolitan_thana) VALUES")
    rows = [
        f"    ({u['id']}, {u['district_id']}, {quote(official(u['name']))}, {quote(u['bn_name'])}, false)"
        for u in upazilas
    ]
    add(",\n".join(rows))
    add("ON CONFLICT (id) DO NOTHING;")
    add("")

    district_ids = {official(d["name"]): d["id"] for d in districts}
    taken = {(u["district_id"], official(u["name"]).lower()) for u in upazilas}

    metro_rows = []
    next_id = METRO_ID_START
    for district_name, thanas in METROPOLITAN_THANAS.items():
        if district_name not in district_ids:
            raise SystemExit(f"District {district_name!r} is not in the source dataset")

        district_id = district_ids[district_name]
        for thana in thanas:
            # A few thana names repeat a rural upazila of the same district; the
            # unique index would reject the second one.
            if (district_id, thana.lower()) in taken:
                print(f"skipped {thana} ({district_name}) - already an upazila there")
                continue

            taken.add((district_id, thana.lower()))
            metro_rows.append(f"    ({next_id}, {district_id}, {quote(thana)}, NULL, true)")
            next_id += 1

    add(f"-- {len(metro_rows)} metropolitan thanas")
    add("INSERT INTO upazilas (id, district_id, name, bn_name, is_metropolitan_thana) VALUES")
    add(",\n".join(metro_rows))
    add("ON CONFLICT (id) DO NOTHING;")
    add("")
    add("COMMIT;")
    add("")

    with open(OUTPUT, "w", encoding="utf-8", newline="\n") as file:
        file.write("\n".join(lines))

    print(f"wrote {OUTPUT}: {len(divisions)} divisions, {len(districts)} districts, "
          f"{len(upazilas)} upazilas, {len(metro_rows)} metropolitan thanas")


if __name__ == "__main__":
    main()
