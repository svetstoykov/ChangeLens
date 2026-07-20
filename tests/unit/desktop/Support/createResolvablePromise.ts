interface ResolvablePromise<T> {
  promise: Promise<T>;
  resolve: (value: T | PromiseLike<T>) => void;
  reject: (reason?: unknown) => void;
}

export function createResolvablePromise<T>(): ResolvablePromise<T> {
  let resolve!: ResolvablePromise<T>["resolve"];
  let reject!: ResolvablePromise<T>["reject"];

  const promise = new Promise<T>((promiseResolve, promiseReject) => {
    resolve = promiseResolve;
    reject = promiseReject;
  });

  return { promise, resolve, reject };
}
