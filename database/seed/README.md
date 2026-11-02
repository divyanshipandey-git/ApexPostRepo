# Reference data

There is no standalone seed SQL file. `SeedShippingZonesAndRates` in the EF migrations is the authoritative, repeatable reference-data seed for shipping zones and rates. Other operational records are created only by application workflows or explicit synthetic test fixtures; do not seed fabricated sales, shipments, money orders, identity checks, or audit records into the persistent development database.
