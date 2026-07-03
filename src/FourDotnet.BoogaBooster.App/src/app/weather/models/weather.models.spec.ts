import {
  normalizePrecipitation,
  normalizeRegime,
  sceneFor,
  summaryText,
  toWeatherConditions,
  windDescription,
} from './weather.models';

describe('weather.models', () => {
  describe('enum normalization', () => {
    it('maps numeric precipitation and regime to labels', () => {
      expect(normalizePrecipitation(1)).toBe('Rain');
      expect(normalizePrecipitation(3)).toBe('Hail');
      expect(normalizeRegime(2)).toBe('StrongWind');
      expect(normalizeRegime(0)).toBe('Calm');
    });

    it('passes string precipitation and regime through (case-insensitive)', () => {
      expect(normalizePrecipitation('Snow')).toBe('Snow');
      expect(normalizePrecipitation('rain')).toBe('Rain');
      expect(normalizeRegime('StrongWind')).toBe('StrongWind');
    });

    it('falls back for unknown values', () => {
      expect(normalizePrecipitation(99)).toBe('None');
      expect(normalizeRegime('nope')).toBe('Calm');
    });

    it('maps a raw DTO with numeric enums onto normalized conditions', () => {
      const conditions = toWeatherConditions({
        temperatureCelsius: 12.5,
        windBeaufort: 4,
        sunshinePercent: 40,
        precipitation: 1,
        regime: 1,
        niceWeather: 0.2,
      });

      expect(conditions.precipitation).toBe('Rain');
      expect(conditions.regime).toBe('Precipitation');
      expect(conditions.niceWeather).toBe(0.2);
    });
  });

  describe('derivations', () => {
    it('describes wind on the Beaufort scale', () => {
      expect(windDescription(2)).toBe('Light breeze');
      expect(windDescription(9)).toBe('Strong gale');
    });

    it('derives the scene from the conditions', () => {
      expect(sceneFor(null).kind).toBe('calm');
      expect(
        sceneFor({
          temperatureCelsius: 14,
          windBeaufort: 9,
          sunshinePercent: 25,
          precipitation: 'None',
          regime: 'StrongWind',
          niceWeather: 0,
        }).kind,
      ).toBe('wind');
      expect(
        sceneFor({
          temperatureCelsius: 12,
          windBeaufort: 4,
          sunshinePercent: 40,
          precipitation: 'Snow',
          regime: 'Precipitation',
          niceWeather: 0.3,
        }).kind,
      ).toBe('snow');
    });

    it('summarizes conditions in words', () => {
      const text = summaryText({
        temperatureCelsius: 20,
        windBeaufort: 2,
        sunshinePercent: 70,
        precipitation: 'None',
        regime: 'Calm',
        niceWeather: 1,
      });

      expect(text).toContain('20 °C');
      expect(text).toContain('light breeze');
      expect(text).toContain('pleasant');
    });
  });
});
