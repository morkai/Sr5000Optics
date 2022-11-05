# Sr5000Optics

## Configuration

Configuration file is specific to a production line and contains info
about the code reader and available luminary configurations.

First, the app tries to read the `config.production.json` file from the
app's root directory and fallbacks to `config.json` if it doesn't exist.

The config file has the following structure:

```json
{
  "bank": 0,
  "ledsPrefix": "7",
  "opticsPrefix": "6",
  "ipAddress": "192.168.100.100",
  "connectRetryCount": 3,
  "connectRetryDelay": 2000,
  "triggerRetryCount": 3,
  "triggerRetryDelay": 333,
  "luminaries": {
    "TEST": [
      // 2x LEDS, 8x OPTICS
      {
        "leds": {
          "7100": [
            {"x1": 943, "y1": 1423, "x2": 1054, "y2": 1518},
            {"x1": 1726, "y1": 1441, "x2": 1853, "y2": 1539}
          ]
        },
        "optics": {
          "6110": [{"x1": 380, "y1": 1163, "x2": 475, "y2": 1250}],
          "6120": [{"x1": 380, "y1": 867, "x2": 483, "y2": 952}],
          "6130": [{"x1": 394, "y1": 559, "x2": 493, "y2": 648}],
          "6140": [{"x1": 404, "y1": 264, "x2": 499, "y2": 349}],
          "6210": [{"x1": 1189, "y1": 1193, "x2": 1278, "y2": 1280}],
          "6220": [{"x1": 1191, "y1": 881, "x2": 1296, "y2": 978}],
          "6230": [{"x1": 1203, "y1": 585, "x2": 1299, "y2": 682}],
          "6240": [{"x1": 1217, "y1": 286, "x2": 1306, "y2": 373}]
        }
      }
    ]
  }
}
```

where:

* `bank` - a parameter bank number to use during a trigger operation.
* `ledsPrefix` - a component item prefix used to distinguish LEDs.
* `opticsPrefix` - a component item prefix used to distinguish optics.
* `ipAddress` - an address of the coder scanner.
* `connectRetryCount` - a number of times the connection establishment should
  be retries after failure.
* `connectRetryDelay` - a number of milliseconds between connection retries.
* `triggerRetryCount` - a number of times the trigger operation should be
  performed if we haven't recognized all required codes yet.
* `triggerRetryDelay` - a number of milliseconds between trigger retries.
* `luminaries` - a map of luminaries to a list of possible configurations.
  Each luminary configuration contains definitions of areas containing
  codes to be read by the code reader.

## Running

The app can be run by executing the `Sr5000Optics.exe` executable with
a path to an input config file or by specifying all required input config
as arguments:

```
Sr5000Optics.exe --f C:\Temp\sr5000.json
```

The input config file has the following structure:

```json
{
  "bank": -1,
  "luminary": "TEST",
  "components": [
    {"item": "6110", "material":  "607341", "quantity": 1},
    {"item": "6120", "material":  "607341", "quantity": 1},
    {"item": "6130", "material":  "607341", "quantity": 1},
    {"item": "6140", "material":  "607341", "quantity": 1},
    {"item": "6210", "material":  "607341", "quantity": 1},
    {"item": "6220", "material":  "607341", "quantity": 1},
    {"item": "6230", "material":  "607341", "quantity": 1},
    {"item": "6240", "material":  "607341", "quantity": 1},
    {"item": "7100", "material":  "442295461301", "quantity": 2}
  ]
}
```

where:

* `bank` - an optional parameter bank number to use during a trigger
  operation. If not specified, the default one is used.
* `luminary` - a luminary ID to use to determine area definitions.
* `components` - a components list that is used to determine areas
  of all the LED and optics codes to check.
* `components.item` - a component item number. Must match one of the
  configurations of the specified luminary.
* `components.material` - a component material number (12NC).
  This value must exist in the code read from one of the defined
  areas to pass.
* `components.quantity` - a number of components. Luminary configuration
  must have definitions for an equal number of areas.

Input configuration can be overwritten through arguments:

* `--config` or `--f` - a path to an input configuration file.
* `--operation` or `--op` - an operation to run (`trigger` or `areas`,
  defaults to `trigger`).
* `--bank` or `--b` - a parameter bank number to use during a trigger
  operation.
* `--luminary` or `--l` - a luminary ID to use to determine area
  definitions.
* `--component` or `--bom` - a component definition in the following
  format: `<item>/<material>/<quantity>`. Repeatable.

The app writes debug messages to the stderr and the final result to stdout.

If the operation fails, the app exits with code 1 and the last stderr
line contains the error.

If the operation succeeds, the app exits with code 0, the stdout contains
`OK` or `NOK` string and the result image is saved to the `latest.jpg` file
in the same directory the `Sr5000Optics.exe` is located (if there already is
a `latest.jpg` file, it is moved to `previous.jpg`).

`OK` result means that all codes were read and match the specified materials.

`NOK` result means that not all codes were read or at least one code doesn't
match the specified material.
