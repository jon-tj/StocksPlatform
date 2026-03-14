import { Component, OnInit, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { PollService, type PollQuestion, type PollData, type PollAnswerDto } from '../../services/poll.service';

@Component({
  selector: 'app-poll',
  imports: [RouterLink, FormsModule],
  templateUrl: 'poll.html',
  styleUrl: 'poll.css',
})
export class Poll implements OnInit {
  private pollService = inject(PollService);

  poll: PollData | null = null;
  loading = true;
  submitted = false;
  submitting = false;
  error = '';
  answers: Record<number, string> = {};

  get sections(): { name: string; questions: PollQuestion[] }[] {
    if (!this.poll) return [];
    const map = new Map<string, PollQuestion[]>();
    for (const q of this.poll.questions) {
      if (!map.has(q.section)) map.set(q.section, []);
      map.get(q.section)!.push(q);
    }
    return Array.from(map.entries()).map(([name, questions]) => ({ name, questions }));
  }

  get allAnswered(): boolean {
    return (this.poll?.questions.every(q => this.answers[q.id] !== undefined) ?? false);
  }

  ngOnInit() {
    this.pollService.getCurrent().subscribe({
      next: (data) => {
        this.poll = data;
        for (const q of data.questions) {
          if (q.pollType === 1) this.answers[q.id] = '50';
        }
        this.loading = false;
      },
      error: () => {
        this.error = 'Failed to load poll.';
        this.loading = false;
      },
    });
  }

  sliderValue(id: number): number {
    return this.answers[id] !== undefined ? +this.answers[id] : 50;
  }

  setAnswer(id: number, value: string) {
    this.answers = { ...this.answers, [id]: value };
  }

  onSliderChange(id: number, event: Event) {
    this.answers = { ...this.answers, [id]: (event.target as HTMLInputElement).value };
  }

  submit() {
    if (!this.poll || !this.allAnswered || this.submitting) return;
    this.submitting = true;
    const dtos: PollAnswerDto[] = Object.entries(this.answers).map(([id, value]) => ({
      questionId: +id,
      value,
    }));
    this.pollService.submitResponses(this.poll.pollId, dtos).subscribe({
      next: () => { this.submitted = true; this.submitting = false; },
      error: () => { this.error = 'Submission failed. Please try again.'; this.submitting = false; },
    });
  }
}
