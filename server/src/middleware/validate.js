'use strict';

const { ZodError } = require('zod');

/**
 * Express middleware factory that validates req[part] against a zod schema.
 * On success the parsed (coerced) value replaces req[part]. On failure it
 * responds 400 with a readable error message.
 */
function validate(schema, part = 'body') {
  return (req, res, next) => {
    const result = schema.safeParse(req[part]);
    if (!result.success) {
      const message = result.error instanceof ZodError
        ? result.error.issues.map((i) => `${i.path.join('.') || part}: ${i.message}`).join('; ')
        : 'invalid request';
      return res.status(400).json({ error: 'validation_error', message });
    }
    req[part] = result.data;
    return next();
  };
}

module.exports = { validate };
