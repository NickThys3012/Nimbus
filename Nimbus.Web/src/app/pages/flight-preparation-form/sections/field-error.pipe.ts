import { Pipe, PipeTransform } from '@angular/core';
import { type ValidationErrors } from '@angular/forms';

/**
 * Turns a control's `errors` into the short inline message shown under an invalid field across
 * all nine flight-preparation sections (issue #32 mockup frames 2A/4A, e.g. "Required" / "Must be
 * 0 or more"). Centralized here instead of duplicated per section so every section reads the same
 * wording for the same validator.
 */
@Pipe({ name: 'fieldError' })
export class FieldErrorPipe implements PipeTransform {
  transform(errors: ValidationErrors | null | undefined): string | null {
    if (!errors) return null;
    if (errors['required'] || errors['requiredTrue']) return 'Required';
    if (errors['min']) return `Must be ${errors['min'].min} or more`;
    if (errors['max']) return `Must be ${errors['max'].max} or less`;
    return 'Invalid value';
  }
}
