import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

const API = 'http://localhost:5156';

export interface PollQuestion {
  id: number;
  section: string;
  text: string;
  pollType: 0 | 1; // 0 = Binary, 1 = Probability
}

export interface PollData {
  pollId: string;
  questions: PollQuestion[];
}

export interface PollAnswerDto {
  questionId: number;
  value: string;
}

@Injectable({ providedIn: 'root' })
export class PollService {
  private http = inject(HttpClient);

  getCurrent(): Observable<PollData> {
    return this.http.get<PollData>(`${API}/api/poll/current`);
  }

  submitResponses(pollId: string, answers: PollAnswerDto[]): Observable<void> {
    return this.http.post<void>(`${API}/api/poll/${pollId}/responses`, answers);
  }
}
