import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { StudySessions } from './StudySessions';

// Unit tests for the plain validation function: no rendering, no network.
describe('StudySessions.validate', () => {
  const validForm = {
    course: 'Software Development I',
    topic: 'Testing',
    location: 'MIF, room 401',
    startsAt: '2030-01-01T18:00',
    hostName: 'Ieva',
    seatsAvailable: 3
  };

  it('accepts a complete form', () => {
    expect(StudySessions.validate(validForm)).toEqual({});
  });

  it('reports every empty required field', () => {
    const errors = StudySessions.validate({ ...validForm, course: '', topic: ' ', hostName: '' });

    expect(errors).toEqual({
      course: 'Course is required.',
      topic: 'Topic is required.',
      hostName: 'Host name is required.'
    });
  });

  it('rejects a start time in the past', () => {
    const errors = StudySessions.validate({ ...validForm, startsAt: '2020-01-01T18:00' });

    expect(errors.startsAt).toBe('Starts at must be in the future.');
  });
});

// Component tests: render the real component in jsdom (a browser simulated in Node) and
// use it the way a user would - find inputs by their label, buttons by their role.
// fetch is replaced by a Jest mock, so no API has to run.
describe('StudySessions form', () => {
  const json = (status, body) => Promise.resolve({ ok: status < 400, status, json: () => Promise.resolve(body) });
  const existingSession = {
    id: 1, course: 'Software Development I', topic: 'Pull request etiquette', location: 'MIF, room 401',
    startsAt: '2030-01-01T16:00:00Z', hostName: 'Ieva', seatsAvailable: 3, status: 'Scheduled', hostRating: null
  };

  beforeEach(() => {
    global.fetch = jest.fn(() => json(200, [existingSession]));
  });

  afterEach(() => {
    delete global.fetch;
  });

  it('shows client-side errors and does not call the API when the form is empty', async () => {
    render(<StudySessions />);
    await screen.findByText('Pull request etiquette');

    fireEvent.click(screen.getByRole('button', { name: 'Create session' }));

    expect(screen.getByText('Topic is required.')).toBeTruthy();
    // Only the initial GET of the list - no POST was sent.
    expect(global.fetch).toHaveBeenCalledTimes(1);
  });

  it('shows the API\'s validation error next to the field it belongs to', async () => {
    render(<StudySessions />);
    await screen.findByText('Pull request etiquette');
    fireEvent.change(screen.getByLabelText('Course'), { target: { value: 'Software Development I' } });
    fireEvent.change(screen.getByLabelText('Topic'), { target: { value: 'Testing' } });
    fireEvent.change(screen.getByLabelText('Location'), { target: { value: 'MIF, room 401' } });
    fireEvent.change(screen.getByLabelText('Starts at'), { target: { value: '2030-01-01T18:00' } });
    fireEvent.change(screen.getByLabelText('Host name'), { target: { value: 'Ieva' } });
    global.fetch.mockImplementationOnce(() =>
      json(400, { errors: { Location: ['Location is already booked at that time.'] } }));

    fireEvent.click(screen.getByRole('button', { name: 'Create session' }));

    expect(await screen.findByText('Location is already booked at that time.')).toBeTruthy();
    expect(global.fetch).toHaveBeenLastCalledWith('api/studysessions', expect.objectContaining({ method: 'POST' }));
  });
});
