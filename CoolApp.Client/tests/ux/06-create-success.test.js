import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { StudySessions } from '../../src/components/StudySessions';

// UX fix 6: a successful create ends with a confirmation and the new row highlighted.
describe('UX fix 6: creating a session has a clear ending', () => {
  const json = (status, body) => Promise.resolve({ ok: status < 400, status, json: () => Promise.resolve(body) });
  const created = {
    id: 7, course: 'Software Development I', topic: 'Testing', location: 'MIF, room 401',
    startsAt: '2030-01-01T16:00:00Z', hostName: 'Ieva', seatsAvailable: 1, status: 'Scheduled', hostRating: null
  };

  beforeEach(() => {
    global.fetch = jest.fn(() => json(200, []));
  });

  afterEach(() => {
    delete global.fetch;
  });

  it('confirms the new session, highlights its row and gets ready for the next one', async () => {
    render(<StudySessions />);
    await screen.findByRole('button', { name: 'Create session' });
    fireEvent.change(screen.getByLabelText('Course'), { target: { value: 'Software Development I' } });
    fireEvent.change(screen.getByLabelText('Topic'), { target: { value: 'Testing' } });
    fireEvent.change(screen.getByLabelText('Location'), { target: { value: 'MIF, room 401' } });
    fireEvent.change(screen.getByLabelText('Starts at'), { target: { value: '2030-01-01T18:00' } });
    fireEvent.change(screen.getByLabelText('Host name'), { target: { value: 'Ieva' } });
    global.fetch
      .mockImplementationOnce(() => json(201, created))   // the POST
      .mockImplementationOnce(() => json(200, [created])); // the list, with the new session

    fireEvent.click(screen.getByRole('button', { name: 'Create session' }));

    expect((await screen.findByRole('status')).textContent).toContain('Session "Testing" created');
    expect((await screen.findByText('Testing')).closest('tr').className).toContain('table-success');
    expect(document.activeElement).toBe(screen.getByLabelText('Course'));
  });
});
