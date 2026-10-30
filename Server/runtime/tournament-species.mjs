// Generated from the fishing weight/chance tables. Maximum includes the 50% hotspot bonus.
// Only explicit gameplay variants share tournament targets. Their actual catch
// weights score at the same 100 points/kg; no variant multiplier is applied.
export const tournamentSpeciesId = id => ({4:5,20:19,23:22,26:24,29:28,34:33})[id] ?? id;
export const tournamentTargets = island => island.fish.filter(f => tournamentSpeciesId(f.id)===f.id);
export const tournamentIslands = [
  {
    "id": 0,
    "name": "Suncrest Reef",
    "fish": [
      {
        "id": 0,
        "name": "Blue Mackerel",
        "maxKg": 3.4499999999999997
      },
      {
        "id": 1,
        "name": "Red Snapper",
        "maxKg": 4.800000000000001
      },
      {
        "id": 2,
        "name": "Sea Bass",
        "maxKg": 7.199999999999999
      },
      {
        "id": 3,
        "name": "Yellowtail",
        "maxKg": 9.75
      },
      {
        "id": 6,
        "name": "Yellow Goatfish",
        "maxKg": 3.3000000000000003
      },
      {
        "id": 7,
        "name": "Black Spot Goatfish",
        "maxKg": 2.7
      },
      {
        "id": 8,
        "name": "Bigeye Tuna",
        "maxKg": 9.0
      },
      {
        "id": 16,
        "name": "Yellowtail Snapper",
        "maxKg": 6.1499999999999995
      },
      {
        "id": 17,
        "name": "Mutton Snapper",
        "maxKg": 9.75
      },
      {
        "id": 21,
        "name": "Mangrove Snapper",
        "maxKg": 8.25
      },
      {"id": 27, "name": "Winter Flounder", "maxKg": 2.312807183365},
      {"id": 28, "name": "Roughtail Stingray", "maxKg": 2.312807183365},
      {"id": 29, "name": "Leopard Stingray", "maxKg": 2.312807183365},
      {"id": 31, "name": "Picasso Triggerfish", "maxKg": 0.84},
      {"id": 32, "name": "Titan Triggerfish", "maxKg": 16.98},
      {"id": 33, "name": "Emperor Angelfish", "maxKg": 2.037861402},
      {"id": 34, "name": "Juvenile Emperor Angelfish", "maxKg": 2.037861402},
      {"id": 35, "name": "Panther Grouper", "maxKg": 10.689598570},
      {"id": 36, "name": "Coral Grouper", "maxKg": 2.517108640},
      {"id": 37, "name": "Blue Lined Grouper", "maxKg": 0.524688812},
      {"id": 39, "name": "Napoleon Wrasse", "maxKg": 233.490300188},
      {"id": 41, "name": "Blue Tang", "maxKg": 0.923550354},
      {"id": 42, "name": "Dussumieri Tang", "maxKg": 5.709815301}
    ]
  },
  {
    "id": 1,
    "name": "Brinebreak Isle",
    "fish": [
      {
        "id": 0,
        "name": "Blue Mackerel",
        "maxKg": 4.5
      },
      {
        "id": 1,
        "name": "Red Snapper",
        "maxKg": 10.5
      },
      {
        "id": 2,
        "name": "Sea Bass",
        "maxKg": 11.52
      },
      {
        "id": 3,
        "name": "Yellowtail",
        "maxKg": 19.5
      },
      {
        "id": 5,
        "name": "Yellowfin Tuna",
        "maxKg": 30.0
      },
      {
        "id": 6,
        "name": "Yellow Goatfish",
        "maxKg": 6.0
      },
      {
        "id": 7,
        "name": "Black Spot Goatfish",
        "maxKg": 7.3500000000000005
      },
      {
        "id": 8,
        "name": "Bigeye Tuna",
        "maxKg": 22.5
      },
      {
        "id": 9,
        "name": "Bonito",
        "maxKg": 12.0
      },
      {
        "id": 10,
        "name": "Black Sea Bass",
        "maxKg": 4.800000000000001
      },
      {
        "id": 11,
        "name": "Striped Bass",
        "maxKg": 42.0
      },
      {
        "id": 12,
        "name": "Spotted Sand Bass",
        "maxKg": 4.800000000000001
      },
      {
        "id": 13,
        "name": "Albacore",
        "maxKg": 22.5
      },
      {
        "id": 14,
        "name": "Greater Amberjack",
        "maxKg": 27.0
      },
      {
        "id": 15,
        "name": "Blacktip Shark",
        "maxKg": 21.0
      },
      {
        "id": 17,
        "name": "Mutton Snapper",
        "maxKg": 19.5
      },
      {
        "id": 18,
        "name": "Blacktip Reef Shark",
        "maxKg": 27.0
      },
      {"id": 40, "name": "White Skate", "maxKg": 2.312807184}
    ]
  },
  {
    "id": 2,
    "name": "Deep Ocean",
    "fish": [
      {
        "id": 0,
        "name": "Blue Mackerel",
        "maxKg": 7.5
      },
      {
        "id": 3,
        "name": "Yellowtail",
        "maxKg": 21.9375
      },
      {
        "id": 5,
        "name": "Yellowfin Tuna",
        "maxKg": 94.5
      },
      {
        "id": 8,
        "name": "Bigeye Tuna",
        "maxKg": 135.0
      },
      {
        "id": 9,
        "name": "Bonito",
        "maxKg": 16.875
      },
      {
        "id": 10,
        "name": "Black Sea Bass",
        "maxKg": 6.75
      },
      {
        "id": 13,
        "name": "Albacore",
        "maxKg": 67.5
      },
      {
        "id": 15,
        "name": "Blacktip Shark",
        "maxKg": 45.0
      },
      {
        "id": 19,
        "name": "Mako Shark",
        "maxKg": 225.0
      }
    ]
  },
  {
    "id": 3,
    "name": "Bluewater Cay",
    "fish": [
      {"id": 3, "name": "Yellowtail", "maxKg": 21.9375},
      {"id": 15, "name": "Blacktip Shark", "maxKg": 45.0},
      {
        "id": 0,
        "name": "Blue Mackerel",
        "maxKg": 6.0
      },
      {
        "id": 5,
        "name": "Yellowfin Tuna",
        "maxKg": 67.5
      },
      {
        "id": 8,
        "name": "Bigeye Tuna",
        "maxKg": 90.0
      },
      {
        "id": 9,
        "name": "Bonito",
        "maxKg": 15.0
      },
      {
        "id": 13,
        "name": "Albacore",
        "maxKg": 52.5
      },
      {
        "id": 19,
        "name": "Mako Shark",
        "maxKg": 225.0
      },
      {
        "id": 24,
        "name": "Bluefin Trevally",
        "maxKg": 82.5
      },
      {
        "id": 25,
        "name": "Golden Trevally",
        "maxKg": 82.5
      },
      {
        "id": 26,
        "name": "Giant Trevally",
        "maxKg": 148.5
      }
    ]
  },
  {
    "id": 4,
    "name": "Snapper Island",
    "fish": [
      {"id": 38, "name": "Nassau Grouper", "maxKg": 37.5},
      {
        "id": 1,
        "name": "Red Snapper",
        "maxKg": 4.800000000000001
      },
      {
        "id": 16,
        "name": "Yellowtail Snapper",
        "maxKg": 6.1499999999999995
      },
      {
        "id": 17,
        "name": "Mutton Snapper",
        "maxKg": 9.75
      },
      {
        "id": 18,
        "name": "Blacktip Reef Shark",
        "maxKg": 27.0
      },
      {
        "id": 21,
        "name": "Mangrove Snapper",
        "maxKg": 15.75
      },
      {
        "id": 22,
        "name": "Blackfin Barracuda",
        "maxKg": 45.0
      },
      {
        "id": 23,
        "name": "Great Barracuda",
        "maxKg": 72.0
      },
      {"id": 30, "name": "Mahogany Snapper", "maxKg": 6.1499999999999995}
    ]
  }
];
