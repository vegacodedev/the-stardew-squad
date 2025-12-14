# Creating a Content Patcher Modpack for The Stardew Squad

This guide explains how to create a Content Patcher mod that customizes NPC behavior, dialogue, and sprites for **The Stardew Squad**.

## Table of Contents

**Getting Started**
- [File Structure](#file-structure) | [content.json Basics](#contentjson-basics)

**Dialogue**
- [Level 1a: Basic Dialogue](#level-1a-basic-dialogue)
- [Level 1b: Task-Specific Dialogue](#level-1b-task-specific-dialogue)
- [Level 1c: Combat & Mining Dialogue](#level-1c-combat--mining-dialogue)
- [Level 1d: Fishing & Farm Dialogue](#level-1d-fishing--farm-dialogue)
- [Level 4: Conditional Dialogue (Heart Levels)](#level-4-conditional-dialogue-heart-levels)
- [Level 5: Exclusive Dialogue (Spouse vs Non-Spouse)](#level-5-exclusive-dialogue-spouse-vs-non-spouse)
- [Level 6: Location-Specific Dialogue](#level-6-location-specific-dialogue)

**Idle Animations**
- [Level 2: Idle Animations](#level-2-idle-animations)
- [Level 3: Idle Animations with Loop Control](#level-3-idle-animations-with-loop-control)
- [Level 14: Conditional Idle Animations](#level-14-conditional-idle-animations)

**Behavior & Recruitment**
- [Level 7: Restrict Allowed Tasks](#level-7-restrict-allowed-tasks)
- [Level 8: Conditional Allowed Tasks](#level-8-conditional-allowed-tasks)
- [Level 9: Recruitment Requirements](#level-9-recruitment-requirements)
- [Level 10: Disable Recruitment Entirely](#level-10-disable-recruitment-entirely)

**Custom Sprites**
- [Level 11: Custom Sprites (Simple)](#level-11-custom-sprites-simple)
- [Level 12: Custom Sprites with Extension Sheet](#level-12-custom-sprites-with-extension-sheet)
- [Level 13: Sprite Flipping](#level-13-sprite-flipping-mirror-left-from-right)

**Pets**
- [Level 15: Pet Configuration (Species-Wide)](#level-15-pet-configuration-species-wide)
- [Level 16: Pet Configuration (Breed-Specific)](#level-16-pet-configuration-breed-specific)

**Advanced**
- [Level 17: Full Customization](#level-17-full-customization)

**Reference**
- [Registering Custom Animations](#registering-custom-animations)
- [Key Concepts](#key-concepts)

---

## File Structure

Your modpack needs these files:

```
[CP] YourModpackName/
├── manifest.json
├── content.json
└── i18n/
    └── default.json    (optional, for translations)
```

### manifest.json

```json
{
  "Name": "Your Modpack Name",
  "Author": "Your Name",
  "Version": "1.0.0",
  "Description": "Custom dialogue and behavior for The Stardew Squad",
  "UniqueID": "YourName.YourModpackName",
  "ContentPackFor": {
    "UniqueID": "Pathoschild.ContentPatcher"
  },
  "Dependencies": [
    {
      "UniqueID": "ThaliaFawnheart.TheStardewSquad",
      "MinimumVersion": "0.10.0",
      "IsRequired": true
    }
  ]
}
```

## content.json Basics

Your `content.json` edits the target `ThaliaFawnheart.TheStardewSquad/NpcConfig`:

```json
{
  "Format": "2.7.0",
  "Changes": [
    {
      "Action": "EditData",
      "Target": "ThaliaFawnheart.TheStardewSquad/NpcConfig",
      "Entries": {
        "NpcName": {
          // Configuration goes here
        }
      }
    }
  ]
}
```

---

## Level 1a: Basic Dialogue

Add dialogue without any conditions. Lines are randomly selected from the array.

**Available dialogue types:** `Recruit`, `Dismiss`, `Idle`, `Attacking`, `Mining`, `Fishing_Waiting`, `Fishing_Caught`, `Watering`, `Lumbering`, `Harvesting`, `Foraging`, `Petting`, `FriendshipTooLow`, `RecruitmentRefusal`

```json
"Sam": {
  "Dialogue": {
    "Recruit": ["{{i18n:recruit.sam.1}}", "{{i18n:recruit.sam.2}}"],
    "Dismiss": ["{{i18n:dismiss.sam.1}}"],
    "Idle": ["{{i18n:idle.sam.1}}", "{{i18n:idle.sam.2}}"]
  }
}
```

---

## Level 1b: Task-Specific Dialogue

NPCs say these lines while performing specific tasks.

```json
"Leah": {
  "Dialogue": {
    "Foraging": [
      "{{i18n:foraging.leah.1}}",
      "{{i18n:foraging.leah.1}}"
    ],
    "Harvesting": ["{{i18n:harvesting.leah.1}}"],
    "Lumbering": ["{{i18n:lumbering.leah.1}}"]
  }
}
```

---

## Level 1c: Combat & Mining Dialogue

Dialogue for combat and mining activities.

```json
"Clint": {
  "Dialogue": {
    "Attacking": [
      "{{i18n:attacking.clint.1}}",
      "{{i18n:attacking.clint.1}}"
    ],
    "Mining": ["{{i18n:mining.clint.1}}", "{{i18n:mining.clint.1}}"]
  }
}
```

---

## Level 1d: Fishing & Farm Dialogue

Dialogue for fishing, watering, and animal care. Fishing has two separate dialogue types: one while waiting for a bite, and one after catching a fish.

```json
"Willy": {
  "Dialogue": {
    "Fishing_Waiting": ["{{i18n:fishing_waiting.willy.1}}", "{{i18n:fishing_waiting.willy.2}}"],
    "Fishing_Caught": ["{{i18n:fishing_caught.willy.1}}", "{{i18n:fishing_caught.willy.2}}"],
    "Watering": ["{{i18n:watering.willy.1}}"],
    "Petting": ["{{i18n:petting.willy.1}}"]
  }
}
```

---

## Level 2: Idle Animations

Add custom idle animations that play when the NPC is waiting. Animation IDs must be registered in `Data/animationDescriptions`.

```json
"Maru": {
  "Behavior": {
    "IdleAnimations": ["maru_tinker", "maru_sit"]
  }
}
```

---

## Level 3: Idle Animations with Loop Control

Control whether animations loop or play once using object format.

```json
"Gus": {
  "Behavior": {
    "IdleAnimations": [
      { "Id": "gus_cook", "Loop": true },
      { "Id": "gus_clean", "Loop": false }
    ]
  }
}
```

---

## Level 4: Conditional Dialogue (Heart Levels)

Different dialogue based on friendship level.

**Important:** Dialogue pools all matching conditions together. At 5 hearts, both "warming" lines AND default lines are available.

```json
"Shane": {
  "Dialogue": {
    "Recruit": [
      {
        "Condition": "PLAYER_HEARTS Current Shane 0 4",
        "Lines": [
          "{{i18n:recruit.shane.grumpy.1}}",
          "{{i18n:recruit.shane.grumpy.2}}"
        ]
      },
      {
        "Condition": "PLAYER_HEARTS Current Shane 5 8",
        "Lines": [
          "{{i18n:recruit.shane.warming.1}}",
          "{{i18n:recruit.shane.warming.2}}"
        ]
      },
      {
        "Condition": "PLAYER_HEARTS Current Shane 9",
        "Lines": [
          "{{i18n:recruit.shane.friend.1}}",
          "{{i18n:recruit.shane.friend.2}}"
        ]
      },
      "{{i18n:recruit.shane.default.1}}"
    ]
  }
}
```

---

## Level 5: Exclusive Dialogue (Spouse vs Non-Spouse)

Use negation (`!`) to create mutually exclusive pools. Only one set of lines will be available, never both.

```json
"Penny": {
  "Dialogue": {
    "Recruit": [
      {
        "Condition": "PLAYER_NPC_RELATIONSHIP Current Penny Married",
        "Lines": [
          "{{i18n:recruit.penny.spouse.1}}",
          "{{i18n:recruit.penny.spouse.2}}"
        ]
      },
      {
        "Condition": "!PLAYER_NPC_RELATIONSHIP Current Penny Married",
        "Lines": [
          "{{i18n:recruit.penny.friend.1}}",
          "{{i18n:recruit.penny.friend.2}}"
        ]
      }
    ]
  }
}
```

---

## Level 6: Location-Specific Dialogue

Different dialogue based on where you recruit the NPC.

```json
"Abigail": {
  "Dialogue": {
    "Recruit": [
      {
        "Condition": "LOCATION_NAME Here SeedShop",
        "Lines": ["{{i18n:recruit.abigail.seedshop.1}}"]
      },
      {
        "Condition": "LOCATION_NAME Here Mountain",
        "Lines": ["{{i18n:recruit.abigail.mountain.1}}"]
      },
      "{{i18n:recruit.abigail.default.1}}"
    ]
  }
}
```

---

## Level 7: Restrict Allowed Tasks

Limit which tasks an NPC can perform.

**Available tasks:** `Attacking`, `Mining`, `Fishing`, `Watering`, `Lumbering`, `Harvesting`, `Foraging`, `Petting`

```json
"Harvey": {
  "Behavior": {
    "AllowedTasks": "Foraging, Harvesting, Watering"
  }
}
```

---

## Level 8: Conditional Allowed Tasks

Unlock more tasks at higher friendship levels.

**Important:** First match wins - order matters!

```json
"Elliott": {
  "Behavior": {
    "AllowedTasks": [
      {
        "Condition": "PLAYER_HEARTS Current Elliott 8",
        "Tasks": "Foraging, Harvesting, Fishing, Lumbering"
      },
      "Foraging, Harvesting, Fishing"
    ]
  }
}
```

---

## Level 9: Recruitment Requirements

Require conditions to be met before an NPC can be recruited.

```json
"Haley": {
  "Behavior": {
    "Recruitment": {
      "Condition": "PLAYER_HEARTS Current Haley 4",
      "RefusalDialogueKey": "{{i18n:recruitment_refusal.haley.1}}"
    }
  }
}
```

---

## Level 10: Disable Recruitment Entirely

Prevent an NPC from being recruited at all.

```json
"Vincent": {
  "Behavior": {
    "Recruitment": {
      "Enabled": false,
      "RefusalDialogueKey": "{{i18n:recruitment_refusal.vincent.tooyoung.1}}"
    }
  }
}
```

---

## Level 11: Custom Sprites (Simple)

Use custom frames from the NPC's base sprite sheet. No `ExtensionSheet` = uses vanilla sprite sheet.

**Available sprite types:** `Attacking`, `Mining`, `Fishing`, `Watering`, `Lumbering`, `Harvesting`, `Foraging`, `Petting`, `Sitting`

```json
"Alex": {
  "Sprites": {
    "Attacking": {
      "FramesByDirection": {
        "Down": [1, 0],
        "Right": [5, 4],
        "Up": [9, 8],
        "Left": [13, 12]
      },
      "FrameDuration": 300,
      "Loop": false
    }
  }
}
```

---

## Level 12: Custom Sprites with Extension Sheet

Load a custom sprite sheet for task animations. This is a two-step process:

### Step 1: Load your sprite sheet as a game asset

First, load your PNG file into the game using Content Patcher's `Load` action. Add this to your `content.json` Changes array:

```json
{
  "Action": "Load",
  "Target": "YourModId/Sprites/Emily_Mining",
  "FromFile": "assets/Emily_Mining.png"
}
```

This loads `assets/Emily_Mining.png` from your mod folder and makes it available as the game asset `YourModId/Sprites/Emily_Mining`.

Your folder structure should look like:

```
[CP] YourModpackName/
├── manifest.json
├── content.json
└── assets/
    └── Emily_Mining.png
```

### Step 2: Reference the asset in ExtensionSheet

Now reference this loaded asset in your NPC config:

```json
"Emily": {
  "Sprites": {
    "Mining": {
      "ExtensionSheet": "YourModId/Sprites/Emily_Mining",
      "FramesByDirection": {
        "Down": [0, 1, 2, 3],
        "Right": [4, 5, 6, 7],
        "Up": [8, 9, 10, 11],
        "Left": [12, 13, 14, 15]
      },
      "FrameDuration": 150,
      "Loop": false,
      "FallbackFrame": 0
    }
  }
}
```

### Complete Example

Here's a full `content.json` showing both steps together:

```json
{
  "Format": "2.7.0",
  "Changes": [
    // Step 1: Load your custom sprite sheets
    {
      "Action": "Load",
      "Target": "YourModId/Sprites/Emily_Mining",
      "FromFile": "assets/Emily_Mining.png"
    },

    // Step 2: Configure the NPC to use them
    {
      "Action": "EditData",
      "Target": "ThaliaFawnheart.TheStardewSquad/NpcConfig",
      "Entries": {
        "Emily": {
          "Sprites": {
            "Mining": {
              "ExtensionSheet": "YourModId/Sprites/Emily_Mining",
              "FramesByDirection": {
                "Down": [0, 1, 2, 3],
                "Right": [4, 5, 6, 7],
                "Up": [8, 9, 10, 11],
                "Left": [12, 13, 14, 15]
              },
              "FrameDuration": 150,
              "Loop": false,
              "FallbackFrame": 0
            }
          }
        }
      }
    }
  ]
}
```

**Tip:** Use your mod's unique ID in the target path (e.g., `YourName.YourModpack/Sprites/...`) to avoid conflicts with other mods.

---

## Level 13: Sprite Flipping (Mirror Left from Right)

Save sprite sheet space by flipping frames horizontally. Use mixed format: simple integers + objects with `Flip` property.

```json
"Sebastian": {
  "Sprites": {
    "Fishing": {
      "FramesByDirection": {
        "Down": [0, 1],
        "Right": [4, 5],
        "Up": [8, 9],
        "Left": [
          { "Frame": 4, "Flip": true },
          { "Frame": 5, "Flip": true }
        ]
      },
      "FrameDuration": 600,
      "Loop": true
    }
  }
}
```

---

## Level 14: Conditional Idle Animations

Different animations based on conditions. Like dialogue, idle animations pool all matching conditions.

```json
"Krobus": {
  "Behavior": {
    "IdleAnimations": [
      {
        "Condition": "LOCATION_NAME Target MineShaft",
        "Animations": ["krobus_wiggle"]
      },
      {
        "Condition": "TIME 2000 2600",
        "Animations": [{ "Id": "krobus_happy", "Loop": true }]
      },
      "krobus_idle"
    ]
  }
}
```

---

## Level 15: Pet Configuration (Species-Wide)

Use `All_Cat`, `All_Dog`, or `All_Turtle` for species-wide defaults. The `NpcType` field is required for pet configs.

```json
"All_Cat": {
  "NpcType": "Cat",
  "Sprites": {
    "Sitting": {
      "FramesByDirection": {
        "Down": [28, 29],
        "Right": [28, 29],
        "Up": [28, 29],
        "Left": [
          { "Frame": 28, "Flip": true },
          { "Frame": 29, "Flip": true }
        ]
      },
      "FrameDuration": 1000,
      "Loop": true
    }
  },
  "Behavior": {
    "IdleAnimations": [
      { "Id": "TSS_cat_sit", "Loop": false },
      { "Id": "TSS_cat_flop", "Loop": true }
    ]
  }
}
```

---

## Level 16: Pet Configuration (Breed-Specific)

Format: `{PetType}_{BreedName}`. BreedName is a number for vanilla breeds, or a name for custom breeds. Breed-specific config overrides species-wide (`All_Cat`).

```json
"Cat_1": {
  "Behavior": {
    "IdleAnimations": [{ "Id": "TSS_cat_gray_sit", "Loop": true }]
  }
}
```

---

## Level 17: Full Customization

Combining everything: conditional sprites, dialogue, and behavior.

```json
"Demetrius": {
  "Sprites": {
    "Foraging": [
      {
        "Condition": "SEASON Spring",
        "FramesByDirection": {
          "Down": [27, 28, 29],
          "Right": [27, 28, 29],
          "Up": [27, 28, 29],
          "Left": [27, 28, 29]
        },
        "FrameDuration": 200,
        "Loop": false
      },
      {
        "FramesByDirection": {
          "Down": [1, 0],
          "Right": [5, 4],
          "Up": [9, 8],
          "Left": [13, 12]
        },
        "FrameDuration": 300,
        "Loop": false
      }
    ]
  },
  "Dialogue": {
    "Foraging": [
      {
        "Condition": "SEASON Spring",
        "Lines": [
          "{{i18n:foraging.demetrius.spring.1}}",
          "{{i18n:foraging.demetrius.spring.2}}"
        ]
      },
      {
        "Condition": "LOCATION_NAME Target Forest",
        "Lines": ["{{i18n:foraging.demetrius.forest.1}}"]
      },
      "{{i18n:foraging.demetrius.default.1}}",
      "{{i18n:foraging.demetrius.default.2}}"
    ],
    "Idle": [
      {
        "Condition": "PLAYER_NPC_RELATIONSHIP Current Demetrius Married",
        "Lines": ["{{i18n:idle.demetrius.spouse.1}}"]
      },
      "{{i18n:idle.demetrius.science.1}}"
    ]
  },
  "Behavior": {
    "AllowedTasks": [
      {
        "Condition": "PLAYER_HEARTS Current Demetrius 8",
        "Tasks": "Foraging, Harvesting, Mining, Watering"
      },
      "Foraging, Harvesting"
    ],
    "IdleAnimations": [
      {
        "Condition": "LOCATION_NAME Target Farm",
        "Animations": ["demetrius_notes"]
      },
      "demetrius_read",
      { "Id": "demetrius_microscope", "Loop": true }
    ],
    "Recruitment": {
      "Condition": "PLAYER_HEARTS Current Demetrius 2",
      "RefusalDialogueKey": "{{i18n:recruitment_refusal.demetrius.1}}"
    }
  }
}
```

---

## Registering Custom Animations

Custom idle animations must be registered in `Data/animationDescriptions`.

Format: `"startFrames/loopFrames/endFrames"`

```json
{
  "Action": "EditData",
  "Target": "Data/animationDescriptions",
  "Entries": {
    "TSS_cat_sit": "16 17 18 19/20 21 22 23 20 21 22 23 19 19 19 19 19 18 18 18 19 19/19 18 17 16",
    "TSS_cat_flop": "24 25 26/27 27 27 27/26 25 24",
    "TSS_cat_gray_sit": "16 17 18 19/20 21 22 23/19 18 17 16"
  }
}
```

---

## Key Concepts

### Condition Behavior

| Feature             | Behavior                                           |
| ------------------- | -------------------------------------------------- |
| **Dialogue**        | POOLS all matching conditions (multiple can apply) |
| **Idle Animations** | POOLS all matching conditions (multiple can apply) |
| **Allowed Tasks**   | FIRST MATCH WINS (order matters)                   |
| **Sprites**         | FIRST MATCH WINS (order matters)                   |

### Game State Queries

Common conditions you can use:

- `PLAYER_HEARTS Current NpcName MinHearts [MaxHearts]`
- `PLAYER_NPC_RELATIONSHIP Current NpcName RelationType`
- `LOCATION_NAME Here/Target LocationName`
- `SEASON Spring/Summer/Fall/Winter`
- `TIME StartTime EndTime`

### Combining Conditions

Use a comma to combine multiple conditions (AND logic). All conditions must be true:

```json
{
  "Condition": "PLAYER_NPC_RELATIONSHIP Current Penny Married, TIME 600 2199",
  "Lines": ["Good morning, honey! Ready to start the day?"]
}
```

### Negating Conditions

Use `!` prefix to negate any condition:

```json
{
  "Condition": "!PLAYER_NPC_RELATIONSHIP Current Penny Married",
  "Lines": ["Let's head out!"]
}
```

For the full list of game state queries, see the [Stardew Valley Wiki](https://stardewvalleywiki.com/Modding:Game_state_queries).
