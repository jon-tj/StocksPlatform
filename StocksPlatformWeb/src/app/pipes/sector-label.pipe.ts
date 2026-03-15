import { Pipe, PipeTransform } from '@angular/core';

/** Converts "BASIC_MATERIALS" or "basic_materials" → "Basic Materials" */
@Pipe({ name: 'sectorLabel', standalone: true })
export class SectorLabelPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) return '';
    return value
      .split(/[_\s]+/)
      .map(w => w.charAt(0).toUpperCase() + w.slice(1).toLowerCase())
      .join(' ');
  }
}
