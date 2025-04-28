export namespace Json {
    export type JsonValue = string | number | boolean | JsonObject | JsonArray | null;

    export interface JsonObject {
        [x: string]: JsonValue;
    }

    export interface JsonArray extends Array<JsonValue> { }

    export interface IJsonSerializer<T> {
        toJson(value: T): JsonValue;
        fromJson(json: JsonValue): T;
    }

    export class Bool {
        static toJson(value: boolean): JsonValue {
            return value;
        }

        static fromJson(json: JsonValue): boolean {
            return json as boolean;
        }
    }

    export class Number {
        static toJson(value: number): JsonValue {
            return value;
        }

        static fromJson(json: JsonValue): number {
            return json as number;
        }
    }

    export class String {
        static toJson(value: string): JsonValue {
            return value;
        }

        static fromJson(json: JsonValue): string {
            return json as string;
        }
    }

    export class Json {
        static toJson(value: JsonValue): JsonValue {
            return value;
        }

        static fromJson(json: JsonValue): JsonValue {
            return json;
        }
    }

    export function List<T>(itemSerializer: IJsonSerializer<T>): IJsonSerializer<T[]> {
        return {
            toJson(value: T[]): JsonValue {
                return value.map(itemSerializer.toJson);
            },

            fromJson(json: JsonValue): T[] {
                return (json as JsonArray).map(itemSerializer.fromJson);
            },
        };
    }

    export function Dict<T>(itemSerializer: IJsonSerializer<T>): IJsonSerializer<{ [key: string]: T }> {
        return {
            toJson(value: { [key: string]: T }): JsonValue {
                const obj: JsonObject = {};

                for (const key in value) {
                    obj[key] = itemSerializer.toJson(value[key]);
                }

                return obj;
            },

            fromJson(json: JsonValue): { [key: string]: T } {
                const obj: { [key: string]: T } = {};
                const src = json as JsonObject;

                for (const key in src) {
                    obj[key] = itemSerializer.fromJson(src[key]);
                }

                return obj;
            },
        };
    }

    export function Optional<T>(itemSerializer: IJsonSerializer<T>): IJsonSerializer<T | null> {
        return {
            toJson(value: T | null): JsonValue {
                return value === null ? null : itemSerializer.toJson(value);
            },

            fromJson(json: JsonValue): T | null {
                return json === null ? null : itemSerializer.fromJson(json);
            },
        };
    }

    export class DateSerializer {
        static toJson(value: Date): JsonValue {
            const year = pad(value.getUTCFullYear(), 4),
                month = pad(value.getUTCMonth() + 1, 2),
                day = pad(value.getUTCDate(), 2);

            return `${year}-${month}-${day}`;
        }

        static fromJson(json: JsonValue): Date {
            return new Date(json as string);
        }
    }

    export class DateTimeSerializer {
        static toJson(value: Date): JsonValue {
            const year = pad(value.getUTCFullYear(), 4),
                month = pad(value.getUTCMonth() + 1, 2),
                day = pad(value.getUTCDate(), 2),
                hour = pad(value.getUTCHours(), 2),
                min = pad(value.getUTCMinutes(), 2),
                sec = pad(value.getUTCSeconds(), 2),
                msec = pad(value.getUTCMilliseconds(), 3);

            return `${year}-${month}-${day}T${hour}:${min}:${sec}.${msec}Z`;
        }

        static fromJson(json: JsonValue): Date {
            return new Date(json as string);
        }
    }

    export interface IReceiver {
        recv(json: JsonObject): void;
    }

    export interface ISender {
        send(json: JsonObject): void;
    }

    export class Service {
        currentId: number = 0;
        rpcs: Map<number, Rpc> = new Map<number, Rpc>();

        constructor(public sender: ISender) {}

        protected _createRpc(method: string): Rpc {
            const rpc: Rpc = new Rpc(this.currentId, method);
            this.currentId++;
            this.rpcs.set(rpc.id, rpc);
            return rpc;
        }

        protected _findRpc(id: number, method: string): Rpc {
            const rpc = this.rpcs.get(id);
            if (rpc && method != rpc.method) {
                throw new Error(`RPC ${id} method mismatch: remote ${method}, local ${rpc.method}`);
            }
            if (rpc) {
                this.rpcs.delete(id);
                return rpc;
            }
            throw new Error(`RPC id not found: ${id}`);
        }
    }
}

export function cloneDict<T>(data: { [key: string]: T }, copy: (data: T) => T): { [key: string]: T } {
    const obj: { [key: string]: T } = {};

    for (const key in data) {
        obj[key] = copy(data[key]);
    }

    return obj;
}

export function cloneList<T>(data: Array<T>, copy: (data: T) => T): Array<T> {
    return data.map(copy);
}

export function cloneJson(json: Json.JsonValue): Json.JsonValue {
    return JSON.parse(JSON.stringify(json));
}

export function pad(num: number, size: number): string {
    let s = num + '';

    while (s.length < size) {
        s = '0' + s;
    }

    return s;
}

export class Rpc {
    id: number;
    method: string;
    resolve?: (value: any) => void;
    reject?: (error: any) => void;

    constructor(id: number, method: string) {
        this.id = id;
        this.method = method;
    }

    promise<T>() {
        return new Promise<T>((resolve, reject) => {
            this.resolve = resolve;
            this.reject = reject;
        });
    }

    complete(response: any): void {
        if (this.resolve) {
            this.resolve(response);
        }
    }

    fail(error: Error): void {
        if (this.reject) {
            this.reject(error);
        }
    }
}
