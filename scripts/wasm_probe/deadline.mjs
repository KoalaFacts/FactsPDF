export function withDeadline(promise, milliseconds, operation) {
  let timer;
  return Promise.race([promise, new Promise((_, reject) => {
    timer = setTimeout(() => reject(Object.assign(new Error(`${operation} exceeded ${milliseconds} ms`),
      { code: 'B2_TIMEOUT' })), milliseconds);
  })]).finally(() => clearTimeout(timer));
}
