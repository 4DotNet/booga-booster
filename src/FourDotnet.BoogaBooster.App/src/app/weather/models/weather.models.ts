/** The kind of precipitation currently falling. */
export type PrecipitationType = 'None' | 'Rain' | 'Snow' | 'Hail';

/** The active weather regime driving the simulation. */
export type WeatherRegime = 'Calm' | 'Precipitation' | 'StrongWind';

/** Normalized current weather conditions used across the UI. */
export interface WeatherConditions {
  readonly temperatureCelsius: number;
  readonly windBeaufort: number;
  readonly sunshinePercent: number;
  readonly precipitation: PrecipitationType;
  readonly regime: WeatherRegime;
  /** "Nice weather" indicator in the range 0–1 (1 = pleasant, 0 = severe). */
  readonly niceWeather: number;
}

/**
 * The raw server DTO. The `precipitation`/`regime` enums may arrive as numbers
 * (System.Text.Json default) or as strings, so both are accepted here.
 */
export interface WeatherConditionDto {
  readonly temperatureCelsius: number;
  readonly windBeaufort: number;
  readonly sunshinePercent: number;
  readonly precipitation: number | string;
  readonly regime: number | string;
  readonly niceWeather: number;
}

/** A descriptor the panel uses to drive the animated scene. */
export interface WeatherScene {
  readonly kind: 'calm' | 'rain' | 'snow' | 'hail' | 'wind';
  readonly sunshinePercent: number;
  readonly niceWeather: number;
}

const PRECIPITATION_BY_INDEX: readonly PrecipitationType[] = ['None', 'Rain', 'Snow', 'Hail'];
const REGIME_BY_INDEX: readonly WeatherRegime[] = ['Calm', 'Precipitation', 'StrongWind'];

/** Beaufort scale short descriptions, index = Beaufort number (0–12). */
const BEAUFORT_DESCRIPTIONS: readonly string[] = [
  'Calm',
  'Light air',
  'Light breeze',
  'Gentle breeze',
  'Moderate breeze',
  'Fresh breeze',
  'Strong breeze',
  'Near gale',
  'Gale',
  'Strong gale',
  'Storm',
  'Violent storm',
  'Hurricane force',
];

function normalizeEnum<T extends string>(
  value: number | string,
  byIndex: readonly T[],
  fallback: T,
): T {
  if (typeof value === 'number') {
    return byIndex[value] ?? fallback;
  }
  const match = byIndex.find((label) => label.toLowerCase() === value.toLowerCase());
  return match ?? fallback;
}

/** Maps the server's numeric or string precipitation value to a label. */
export function normalizePrecipitation(value: number | string): PrecipitationType {
  return normalizeEnum(value, PRECIPITATION_BY_INDEX, 'None');
}

/** Maps the server's numeric or string regime value to a label. */
export function normalizeRegime(value: number | string): WeatherRegime {
  return normalizeEnum(value, REGIME_BY_INDEX, 'Calm');
}

/** Projects a raw server DTO onto the normalized {@link WeatherConditions}. */
export function toWeatherConditions(dto: WeatherConditionDto): WeatherConditions {
  return {
    temperatureCelsius: dto.temperatureCelsius,
    windBeaufort: dto.windBeaufort,
    sunshinePercent: dto.sunshinePercent,
    precipitation: normalizePrecipitation(dto.precipitation),
    regime: normalizeRegime(dto.regime),
    niceWeather: dto.niceWeather,
  };
}

/** Human-friendly wind description for a Beaufort number. */
export function windDescription(beaufort: number): string {
  const index = Math.min(Math.max(Math.round(beaufort), 0), BEAUFORT_DESCRIPTIONS.length - 1);
  return BEAUFORT_DESCRIPTIONS[index];
}

/** Human-friendly regime label. */
export function regimeLabel(regime: WeatherRegime): string {
  switch (regime) {
    case 'Precipitation':
      return 'Precipitation';
    case 'StrongWind':
      return 'Strong wind';
    default:
      return 'Calm';
  }
}

/** Human-friendly precipitation label. */
export function precipitationLabel(precipitation: PrecipitationType): string {
  return precipitation === 'None' ? 'None' : precipitation;
}

/** Qualitative label for the nice-weather indicator (0–1). */
export function nicenessLabel(niceWeather: number): string {
  const value = clamp01(niceWeather);
  if (value >= 0.8) {
    return 'Pleasant';
  }
  if (value >= 0.5) {
    return 'Fair';
  }
  if (value >= 0.25) {
    return 'Poor';
  }
  return 'Severe';
}

/** Builds the animated-scene descriptor from the current conditions. */
export function sceneFor(conditions: WeatherConditions | null): WeatherScene {
  if (!conditions) {
    return { kind: 'calm', sunshinePercent: 0, niceWeather: 0 };
  }

  const base = {
    sunshinePercent: conditions.sunshinePercent,
    niceWeather: conditions.niceWeather,
  };

  if (conditions.regime === 'StrongWind') {
    return { kind: 'wind', ...base };
  }
  switch (conditions.precipitation) {
    case 'Rain':
      return { kind: 'rain', ...base };
    case 'Snow':
      return { kind: 'snow', ...base };
    case 'Hail':
      return { kind: 'hail', ...base };
    default:
      return { kind: 'calm', ...base };
  }
}

/** A one-line, worded summary of the conditions for a live region. */
export function summaryText(conditions: WeatherConditions | null): string {
  if (!conditions) {
    return 'Weather conditions are unavailable.';
  }
  const parts = [
    `${formatTemperature(conditions.temperatureCelsius)}`,
    windDescription(conditions.windBeaufort).toLowerCase(),
    `${conditions.sunshinePercent}% sunshine`,
  ];
  if (conditions.precipitation !== 'None') {
    parts.push(precipitationLabel(conditions.precipitation).toLowerCase());
  }
  return `${parts.join(', ')} — ${nicenessLabel(conditions.niceWeather).toLowerCase()}.`;
}

/** Formats a temperature for display, e.g. `20.5 °C`. */
export function formatTemperature(celsius: number): string {
  return `${Math.round(celsius * 10) / 10} °C`;
}

/** Clamps a value into the 0–1 range. */
export function clamp01(value: number): number {
  return Math.min(Math.max(value, 0), 1);
}
