import React from 'react';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { StudySessions } from '../../src/components/StudySessions';

// UX fix 1: every request has four states - waiting, slow, failed and done.
describe('UX fix 1: the session list has four loading states', () => {
  const json = (status, body) => Promise.resolve({ ok: status < 400, status, json: () => Promise.resolve(body) });
  const session = {
    id: 1, course: 'Software Development I', topic: 'Pull request etiquette', location: 'MIF, room 401',
    startsAt: '2030-01-01T16:00:00Z', hostName: 'Ieva', seatsAvailable: 3, status: 'Scheduled', hostRating: null
  };

  afterEach(() => {
    jest.useRealTimers();
    delete global.fetch;
  });

  it('says "Still loading..." when the list takes longer than 1 second', () => {
    jest.useFakeTimers();
    global.fetch = jest.fn(() => new Promise(() => {})); // the server never answers

    render(<StudySessions />);
    expect(screen.getByText('Loading...')).toBeTruthy();

    act(() => jest.advanceTimersByTime(1000));

    expect(screen.getByText('Still loading...')).toBeTruthy();
  });

  it('shows an error instead of "Loading..." forever when the server cannot be reached', async () => {
    global.fetch = jest.fn(() => Promise.reject(new TypeError('Failed to fetch')));

    render(<StudySessions />);

    expect((await screen.findByRole('alert')).textContent).toContain('Could not load the study sessions');
    expect(screen.queryByText('Loading...')).toBeNull();
  });

  it('shows an error when the server answers with a failure', async () => {
    global.fetch = jest.fn(() => json(500, { title: 'An error occurred' }));

    render(<StudySessions />);

    expect((await screen.findByRole('alert')).textContent).toContain('Could not load the study sessions');
  });

  it('loads the list again with "Try again"', async () => {
    global.fetch = jest.fn(() => Promise.reject(new TypeError('Failed to fetch')));
    render(<StudySessions />);
    await screen.findByRole('alert');
    global.fetch.mockImplementation(() => json(200, [session]));

    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('Pull request etiquette')).toBeTruthy();
    expect(screen.queryByRole('alert')).toBeNull();
  });
});
