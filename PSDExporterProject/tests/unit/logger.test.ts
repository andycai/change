import { Logger, LogLevel, LogEntry } from '../../src/utils/logger';

describe('Logger', () => {
  let logger: Logger;

  beforeEach(() => {
    logger = new Logger(false);
  });

  test('should create a Logger instance', () => {
    expect(logger).toBeInstanceOf(Logger);
  });

  test('should log info messages', () => {
    const consoleSpy = jest.spyOn(console, 'log').mockImplementation(() => {});
    logger.info('test message');
    expect(consoleSpy).toHaveBeenCalledTimes(1);

    const call = consoleSpy.mock.calls[0][0];
    expect(call).toContain('[INFO]');
    expect(call).toContain('test message');

    consoleSpy.mockRestore();
  });

  test('should log warn messages', () => {
    const consoleSpy = jest.spyOn(console, 'warn').mockImplementation(() => {});
    logger.warn('warning message');
    expect(consoleSpy).toHaveBeenCalledTimes(1);

    const call = consoleSpy.mock.calls[0][0];
    expect(call).toContain('[WARN]');
    expect(call).toContain('warning message');

    consoleSpy.mockRestore();
  });

  test('should log error messages', () => {
    const consoleSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
    logger.error('error message');
    expect(consoleSpy).toHaveBeenCalledTimes(1);

    const call = consoleSpy.mock.calls[0][0];
    expect(call).toContain('[ERROR]');
    expect(call).toContain('error message');

    consoleSpy.mockRestore();
  });

  test('should respect debug mode - off by default', () => {
    const consoleSpy = jest.spyOn(console, 'log').mockImplementation(() => {});
    logger.debug('debug message');
    expect(consoleSpy).not.toHaveBeenCalled();
    consoleSpy.mockRestore();
  });

  test('should output debug messages when debug is enabled', () => {
    const debugLogger = new Logger(true);
    const consoleSpy = jest.spyOn(console, 'log').mockImplementation(() => {});
    debugLogger.debug('debug message');
    expect(consoleSpy).toHaveBeenCalledTimes(1);

    const call = consoleSpy.mock.calls[0][0];
    expect(call).toContain('[DEBUG]');
    expect(call).toContain('debug message');

    consoleSpy.mockRestore();
  });

  test('should include context in log output', () => {
    const consoleSpy = jest.spyOn(console, 'log').mockImplementation(() => {});
    logger.info('test with context', { userId: '123', action: 'login' });
    expect(consoleSpy).toHaveBeenCalledTimes(1);

    const call = consoleSpy.mock.calls[0][0];
    expect(call).toContain('{"userId":"123","action":"login"}');

    consoleSpy.mockRestore();
  });

  test('should include ISO timestamp in log output', () => {
    const consoleSpy = jest.spyOn(console, 'log').mockImplementation(() => {});
    logger.info('timestamp test');
    expect(consoleSpy).toHaveBeenCalledTimes(1);

    const call = consoleSpy.mock.calls[0][0];
    // Should match ISO 8601 format
    expect(call).toMatch(/\[\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/);

    consoleSpy.mockRestore();
  });

  test('should not include context string when no context provided', () => {
    const consoleSpy = jest.spyOn(console, 'log').mockImplementation(() => {});
    logger.info('no context');
    expect(consoleSpy).toHaveBeenCalledTimes(1);

    const call = consoleSpy.mock.calls[0][0];
    // Should not end with JSON after the message
    expect(call).not.toMatch(/\{\}$/);

    consoleSpy.mockRestore();
  });
});
