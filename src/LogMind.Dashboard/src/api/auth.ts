const KEY = 'lm_api_key';

export const auth = {
  get: (): string => localStorage.getItem(KEY) ?? '',
  set: (key: string) => localStorage.setItem(KEY, key),
  clear: () => localStorage.removeItem(KEY),
};
